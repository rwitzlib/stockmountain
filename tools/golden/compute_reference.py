#!/usr/bin/env python
"""Compute independent reference indicator values for every golden bars fixture.

For each TestData/Golden/bars/*.json this writes TestData/Golden/reference/<name>.indicators.json:

  {
    "source": "<bars file>", "generatedBy": "...", "libs": {...}, "barCount": N,
    "series": { "<dsl fragment>": [null, ..., value, ...], ... }   # aligned to bars, null = warm-up
  }

Keys are literal DSL fragments so the C# golden tests evaluate the exact same string via
IndicatorExpressionEngine.EvaluateSeries and compare bar-by-bar.

Seed / smoothing conventions are deliberately written to match the C# contract
(see plans/14-golden-filter-tests.md §1c) — NOT the pandas defaults:
  - ema(n):  SMA seed at bar n-1, then alpha = 2/(n+1)              (EmaFunction.cs)
  - rsi(n,..,wilders): SMA seed of first n gains/losses, alpha=1/n  (RsiFunction.cs)
  - rsi(n,..,ema):     SMA seed, alpha = 2/(n+1)  (non-standard, but the C# contract)
  - rsi(n,..,sma):     rolling means of gains/losses
  - macd(f,s,sig,ema): fast/slow as ema() above; macd from bar s-1; signal = ema() over macd values
                       (SMA seed of the first `sig` macd values); histogram = macd - signal
  - macd(f,s,sig,sma): rolling means throughout
  - adv(n):  rolling mean of volume over the last n bars INCLUDING the current bar
  - slope(x,n): least-squares slope of the last n values against x = 0..n-1
"""
from __future__ import annotations

import json
import math
import subprocess
import sys
from pathlib import Path

import numpy as np
import pandas as pd

REPO = Path(__file__).resolve().parents[2]
GOLDEN = REPO / "tests/marketviewer-filters-unit-tests/MarketViewer.Filters.UnitTests/TestData/Golden"
BARS = GOLDEN / "bars"
REF = GOLDEN / "reference"

SMA_PERIODS = [5, 20, 50, 200]
EMA_PERIODS = [5, 20, 50, 200]
ADV_PERIODS = [20, 30]


def sma(x: pd.Series, n: int) -> pd.Series:
    return x.rolling(n).mean()


def ema(x: pd.Series, n: int) -> pd.Series:
    """SMA-seeded EMA (matches EmaFunction.cs / TA-Lib), NOT pandas ewm default."""
    v = x.to_numpy(dtype=float)
    out = np.full(len(v), np.nan)
    if len(v) < n:
        return pd.Series(out, index=x.index)
    a = 2.0 / (n + 1)
    out[n - 1] = v[:n].mean()
    for i in range(n, len(v)):
        out[i] = (v[i] - out[i - 1]) * a + out[i - 1]
    return pd.Series(out, index=x.index)


def rsi(close: pd.Series, n: int, kind: str) -> pd.Series:
    c = close.to_numpy(dtype=float)
    out = np.full(len(c), np.nan)
    if len(c) < n + 1:
        return pd.Series(out, index=close.index)
    d = np.diff(c)
    gains = np.maximum(d, 0.0)
    losses = np.maximum(-d, 0.0)
    avg_g = gains[:n].mean()
    avg_l = losses[:n].mean()

    def to_rsi(g: float, l: float) -> float:
        if l == 0:
            return 100.0
        return 100.0 - 100.0 / (1.0 + g / l)

    out[n] = to_rsi(avg_g, avg_l)
    alpha = 2.0 / (n + 1)
    for i in range(n + 1, len(c)):
        g, l = gains[i - 1], losses[i - 1]
        if kind == "wilders":
            avg_g = (avg_g * (n - 1) + g) / n
            avg_l = (avg_l * (n - 1) + l) / n
        elif kind == "ema":
            avg_g = (g - avg_g) * alpha + avg_g
            avg_l = (l - avg_l) * alpha + avg_l
        elif kind == "sma":
            avg_g = gains[i - n:i].mean()
            avg_l = losses[i - n:i].mean()
        else:
            raise ValueError(kind)
        out[i] = to_rsi(avg_g, avg_l)
    return pd.Series(out, index=close.index)


def macd(close: pd.Series, fast: int, slow: int, sig: int, kind: str):
    ma = ema if kind == "ema" else sma
    fast_ma = ma(close, fast)
    slow_ma = ma(close, slow)
    line = fast_ma - slow_ma  # NaN until slow-1
    valid = line.dropna()
    signal_valid = ma(valid, sig)  # seeded from the first `sig` MACD values, like the C#
    signal = pd.Series(np.nan, index=close.index)
    signal.loc[signal_valid.index] = signal_valid
    hist = line - signal
    return line, signal, hist


def slope(x: pd.Series, n: int) -> pd.Series:
    v = x.to_numpy(dtype=float)
    out = np.full(len(v), np.nan)
    xs = np.arange(n, dtype=float)
    sx, sx2 = xs.sum(), (xs * xs).sum()
    denom = n * sx2 - sx * sx
    for end in range(n - 1, len(v)):
        w = v[end - n + 1:end + 1]
        if np.isnan(w).any():
            continue
        out[end] = (n * (xs * w).sum() - sx * w.sum()) / denom
    return pd.Series(out, index=x.index)


def to_list(s: pd.Series) -> list:
    return [None if (v is None or (isinstance(v, float) and math.isnan(v))) else round(float(v), 8) for v in s.tolist()]


def git_sha() -> str:
    try:
        return subprocess.check_output(["git", "rev-parse", "--short", "HEAD"], cwd=REPO, text=True).strip()
    except Exception:
        return "unknown"


def compute(bars_file: Path) -> dict:
    raw = json.loads(bars_file.read_text(encoding="utf-8"))
    df = pd.DataFrame(raw["results"])
    # Match the C# numeric path: bars are float32 in Massive.Client.Models.Bar; the C# widens to double
    # per operation. Cast inputs to float32 then back to float64 so the *inputs* are identical.
    close = pd.Series(df["c"].to_numpy(dtype=np.float32).astype(np.float64))
    volume = pd.Series(df["v"].to_numpy(dtype=np.float32).astype(np.float64))

    series: dict[str, list] = {}
    for n in SMA_PERIODS:
        series[f"sma({n})"] = to_list(sma(close, n))
    for n in EMA_PERIODS:
        series[f"ema({n})"] = to_list(ema(close, n))
    for kind in ("wilders", "ema", "sma"):
        series[f"rsi(14,70,30,{kind})"] = to_list(rsi(close, 14, kind))
    series["rsi(2,90,10,wilders)"] = to_list(rsi(close, 2, "wilders"))
    for kind in ("ema", "sma"):
        line, sig, hist = macd(close, 12, 26, 9, kind)
        series[f"macd(12,26,9,{kind}).value"] = to_list(line)
        series[f"macd(12,26,9,{kind}).signal"] = to_list(sig)
        series[f"macd(12,26,9,{kind}).histogram"] = to_list(hist)
    for n in ADV_PERIODS:
        series[f"adv({n})"] = to_list(sma(volume, n))
    series["adv()"] = to_list(sma(volume, 30))
    series["slope(close,5)"] = to_list(slope(close, 5))
    series["slope(sma(20),10)"] = to_list(slope(sma(close, 20), 10))
    series["slope(ema(20),10)"] = to_list(slope(ema(close, 20), 10))

    return {
        "source": bars_file.name,
        "generatedBy": f"tools/golden/compute_reference.py@{git_sha()}",
        "libs": {"python": sys.version.split()[0], "pandas": pd.__version__, "numpy": np.__version__},
        "barCount": len(df),
        "series": series,
    }


def main() -> None:
    REF.mkdir(parents=True, exist_ok=True)
    files = sorted(BARS.glob("*.json"))
    if not files:
        sys.exit(f"no fixtures in {BARS}")
    for f in files:
        out = REF / f"{f.stem}.indicators.json"
        out.write_text(json.dumps(compute(f), separators=(",", ":")), encoding="utf-8")
        print(f"wrote {out.relative_to(REPO)} ({out.stat().st_size/1024:.0f} KB)")


if __name__ == "__main__":
    main()
