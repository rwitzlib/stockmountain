#!/usr/bin/env python
"""Compute expected filter outcomes (layer 2 of plans/14-golden-filter-tests.md).

For each case below, evaluates the filter *independently* — from the reference indicator series
in TestData/Golden/reference/*.indicators.json plus raw bar fields — and writes the set of bar
timestamps where the filter is true into TestData/Golden/outcomes/filters.json. The C# test
(GoldenFilterOutcomeTests) replays the same fixture through FilterSession the way ScannerService
does and must produce the identical set.

Two kinds of case:
  reference — expected computed here (a real golden).
  snapshot  — no reference implementation exists (support_resistance); expected is blessed by
              the C# test when GOLDEN_UPDATE=1 and preserved across re-runs of this script.

Semantics mirrored from MarketViewer.Filters (must stay in sync — these ARE the contract):
  * `a OP b [tf, r, mode]` compares the last r bars for which BOTH sides have a value
    (right-aligned; if fewer than r are available, all available are used; none -> false).
    mode all (default) requires every bar to satisfy OP, mode any requires one.
  * `crosses_over(a,b)` in range r: some bar j in the last r bars has a[j-1] <= b[j-1] and a[j] > b[j].
  * `time` is minutes since midnight America/New_York of the *evaluation clock*
    (the replay passes the current bar's timestamp).
  * Logical AND binds tighter than OR (standard precedence, "a OR b AND c" == "a OR (b AND c)");
    parentheses group explicitly. NOT binds to the single comparison/call after it.
    NOT is unary and takes the following comparison (or parenthesised group) as its operand.

Replay window (mirrored in GoldenReplay.cs):
  * 1-minute fixtures: for every ET trading date in the fixture except the first, seed with all
    bars before 09:30 ET, then feed each regular-session bar (09:30 <= t < 16:00) and evaluate.
  * other fixtures: seed with the first 250 bars, then feed and evaluate each remaining bar.
"""
from __future__ import annotations

import json
import subprocess
import sys
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from zoneinfo import ZoneInfo

import numpy as np

REPO = Path(__file__).resolve().parents[2]
GOLDEN = REPO / "tests/marketviewer-filters-unit-tests/MarketViewer.Filters.UnitTests/TestData/Golden"
OUT = GOLDEN / "outcomes" / "filters.json"
ET = ZoneInfo("America/New_York")
SEED_BARS = 250


# --------------------------------------------------------------------------------------- data

class Fixture:
    def __init__(self, name: str):
        self.name = name
        raw = json.loads((GOLDEN / "bars" / f"{name}.json").read_text(encoding="utf-8"))
        ref = json.loads((GOLDEN / "reference" / f"{name}.indicators.json").read_text(encoding="utf-8"))
        bars = raw["results"]
        self.n = len(bars)
        self.t = np.array([b["t"] for b in bars], dtype=np.int64)
        f32 = lambda k: np.array([b[k] for b in bars], dtype=np.float32).astype(np.float64)
        f64 = lambda k: np.array([b[k] for b in bars], dtype=np.float64)
        # Prices are float32 on Bar; Volume is double (plan 14 follow-up #9).
        self.fields = {"close": f32("c"), "open": f32("o"), "high": f32("h"), "low": f32("l"),
                       "volume": f64("v")}
        self.ref = {k: np.array([np.nan if v is None else v for v in s], dtype=np.float64) for k, s in ref["series"].items()}
        et = [datetime.fromtimestamp(ts / 1000, tz=timezone.utc).astimezone(ET) for ts in self.t]
        self.et_minutes = np.array([d.hour * 60 + d.minute for d in et], dtype=np.float64)
        self.et_date = [d.date() for d in et]
        self.tf = name.split("_")[1]

    def series(self, key: str) -> np.ndarray:
        if key in self.fields:
            return self.fields[key]
        if key == "time":
            return self.et_minutes
        if key in self.ref:
            return self.ref[key]
        raise KeyError(f"{self.name}: no reference series for '{key}' — add it to compute_reference.py")

    def evaluated_indices(self) -> list[int]:
        if self.tf == "1m":
            dates = sorted(set(self.et_date))
            out = []
            for d in dates[1:]:
                for i in range(self.n):
                    if self.et_date[i] == d and 570 <= self.et_minutes[i] < 960:
                        out.append(i)
            return out
        return list(range(SEED_BARS, self.n))


# ---------------------------------------------------------------------------------- evaluator

OPS = {
    ">": np.greater, ">=": np.greater_equal, "<": np.less, "<=": np.less_equal,
    "=": np.equal, "!=": np.not_equal,
}


def _valid(*arrs: np.ndarray) -> np.ndarray:
    v = np.ones_like(arrs[0], dtype=bool)
    for a in arrs:
        v &= ~np.isnan(a)
    return v


def cmp(fx: Fixture, left: str, op: str, right, r: int = 1, mode: str = "all") -> np.ndarray:
    a = fx.series(left)
    b = fx.series(right) if isinstance(right, str) else np.full(fx.n, float(right))
    valid = _valid(a, b)
    per_bar = OPS[op](a, b) & valid
    if left == "time":  # time is a single-point series (the evaluation clock): range is irrelevant
        return per_bar
    out = np.zeros(fx.n, dtype=bool)
    for i in range(fx.n):
        if not valid[i]:
            continue
        # walk back over the contiguous valid run, at most r bars
        js = []
        j = i
        while j >= 0 and valid[j] and len(js) < r:
            js.append(j)
            j -= 1
        vals = per_bar[js]
        out[i] = vals.all() if mode == "all" else vals.any()
    return out


def crosses(fx: Fixture, left: str, right: str, r: int = 1, over: bool = True) -> np.ndarray:
    a, b = fx.series(left), fx.series(right)
    valid = _valid(a, b)
    out = np.zeros(fx.n, dtype=bool)
    for i in range(fx.n):
        hit = False
        for j in range(max(1, i - r + 1), i + 1):
            if not (valid[j] and valid[j - 1]):
                continue
            if over and a[j - 1] <= b[j - 1] and a[j] > b[j]:
                hit = True
            if not over and a[j - 1] >= b[j - 1] and a[j] < b[j]:
                hit = True
        out[i] = hit
    return out


# -------------------------------------------------------------------------------------- cases

@dataclass
class Case:
    id: str
    script: str
    fixtures: list[str]
    pred: object = None                    # callable(Fixture) -> bool array; None for snapshot
    kind: str = "reference"
    known_bug: str | None = None
    note: str = ""


M1 = ["AAPL_1m_2025-06-02_2025-06-06", "NVDA_1m_2025-06-02_2025-06-06"]
M1_ALL = M1 + ["TSLA_1m_2025-03-07_2025-03-11", "SPY_1m_2024-11-27_2024-12-02"]
D1 = ["AAPL_1d_2023-06-01_2025-06-06", "NVDA_1d_2023-06-01_2025-06-06"]
H1 = ["SPY_1h_2025-05-01_2025-05-30"]
RSI = "rsi(14,70,30,wilders)"

CASES: list[Case] = [
    # --- single comparison, literal RHS, each operator
    Case("rsi-oversold", f"{RSI} < 30 [1m]", M1_ALL, lambda f: cmp(f, RSI, "<", 30)),
    Case("rsi-overbought-ge", f"{RSI} >= 70 [1m]", M1, lambda f: cmp(f, RSI, ">=", 70)),
    Case("close-gt-literal", "close > 200 [1m]", ["AAPL_1m_2025-06-02_2025-06-06"], lambda f: cmp(f, "close", ">", 200)),
    Case("close-le-literal", "close <= 140 [1m]", ["NVDA_1m_2025-06-02_2025-06-06"], lambda f: cmp(f, "close", "<=", 140)),
    Case("close-eq-open", "close = open [1m]", M1, lambda f: cmp(f, "close", "=", "open")),
    Case("close-ne-open", "close != open [1m]", M1, lambda f: cmp(f, "close", "!=", "open")),
    Case("literal-lhs", "30 > rsi(14,70,30,wilders) [1m]", M1, lambda f: cmp(f, RSI, "<", 30)),
    # --- ranges and modes
    Case("rsi-overbought-range3-all", f"{RSI} > 70 [1m, 3]", M1, lambda f: cmp(f, RSI, ">", 70, r=3)),
    Case("rsi-ema-range3-any", "rsi(14,70,30,ema) < 35 [1m, 3, any]", M1, lambda f: cmp(f, "rsi(14,70,30,ema)", "<", 35, r=3, mode="any")),
    Case("rsi2-range2-any", "rsi(2,90,10,wilders) < 10 [1m, 2, any]", M1, lambda f: cmp(f, "rsi(2,90,10,wilders)", "<", 10, r=2, mode="any")),
    Case("range-without-tf", f"{RSI} < 30 [, 2]", M1, lambda f: cmp(f, RSI, "<", 30, r=2)),
    Case("mode-all-explicit", "close > sma(20) [1m, 5, all]", M1, lambda f: cmp(f, "close", ">", "sma(20)", r=5)),
    # --- series vs series
    Case("close-gt-sma20", "close > sma(20) [1m]", M1_ALL, lambda f: cmp(f, "close", ">", "sma(20)")),
    Case("sma-stack", "sma(20) > sma(50) [1m]", M1, lambda f: cmp(f, "sma(20)", ">", "sma(50)")),
    Case("volume-gt-adv", "volume > adv(20) [1m]", M1, lambda f: cmp(f, "volume", ">", "adv(20)")),
    Case("close-gt-macd-signal-mixed", "close > macd(12,26,9,ema).signal [1m]", M1,
         lambda f: cmp(f, "close", ">", "macd(12,26,9,ema).signal"),
         note="mixed operand types: data-access series vs dot-field series"),
    Case("macd-value-gt-signal", "macd(12,26,9,ema).value > macd(12,26,9,ema).signal [1m]", M1,
         lambda f: cmp(f, "macd(12,26,9,ema).value", ">", "macd(12,26,9,ema).signal")),
    # --- dot fields / implicit .value
    Case("macd-hist-pos", "macd(12,26,9,ema).histogram > 0 [1m]", M1, lambda f: cmp(f, "macd(12,26,9,ema).histogram", ">", 0)),
    Case("macd-implicit-value", "macd(12,26,9,ema) > 0 [1m]", M1, lambda f: cmp(f, "macd(12,26,9,ema).value", ">", 0)),
    Case("macd-sma-type", "macd(12,26,9,sma).histogram < 0 [1m]", M1, lambda f: cmp(f, "macd(12,26,9,sma).histogram", "<", 0)),
    # --- vwap (session-anchored indicator; the old bare `vwap` literal was Massive's per-bar vw)
    Case("close-gt-vwap", "close > vwap() [1m]", M1_ALL, lambda f: cmp(f, "close", ">", "vwap()")),
    Case("cross-over-vwap-r3", "crosses_over(close, vwap()) [1m, 3]", M1, lambda f: crosses(f, "close", "vwap()", r=3)),
    Case("close-lt-vwap-day", "close < vwap(day) [1m]", M1, lambda f: cmp(f, "close", "<", "vwap(day)")),
    Case("vwap-vs-sma", "vwap() > sma(20) [1m]", M1, lambda f: cmp(f, "vwap()", ">", "sma(20)")),
    Case("close-gt-vwap-1h", "close > vwap() [1h]", H1, lambda f: cmp(f, "close", ">", "vwap()")),
    # --- transforms
    Case("slope-close-pos-and-rsi", f"slope(close,5) > 0 AND {RSI} > 50 [1m]", M1,
         lambda f: cmp(f, "slope(close,5)", ">", 0) & cmp(f, RSI, ">", 50)),
    Case("slope-of-sma", "slope(sma(20),10) > 0 [1m]", M1, lambda f: cmp(f, "slope(sma(20),10)", ">", 0)),
    # --- logical
    Case("and-two", "close > sma(20) AND close > sma(50) [1m]", M1,
         lambda f: cmp(f, "close", ">", "sma(20)") & cmp(f, "close", ">", "sma(50)")),
    Case("and-three", "ema(20) > ema(50) AND ema(50) > ema(200) AND close > ema(20) [1m]", M1,
         lambda f: cmp(f, "ema(20)", ">", "ema(50)") & cmp(f, "ema(50)", ">", "ema(200)") & cmp(f, "close", ">", "ema(20)")),
    Case("or-two", f"sma(20) > sma(50) OR {RSI} < 30 [1m]", M1,
         lambda f: cmp(f, "sma(20)", ">", "sma(50)") | cmp(f, RSI, "<", 30)),
    Case("not-unary", "NOT close > sma(20) [1m]", M1, lambda f: ~cmp(f, "close", ">", "sma(20)")),
    Case("and-then-or", f"close > sma(20) AND {RSI} < 30 OR {RSI} > 70 [1m]", M1,
         lambda f: (cmp(f, "close", ">", "sma(20)") & cmp(f, RSI, "<", 30)) | cmp(f, RSI, ">", 70),
         note="(a AND b) OR c — same under AND-over-OR and a flat fold, so not a precedence witness on its own"),
    Case("or-then-and-precedence", f"{RSI} > 70 OR close > sma(20) AND {RSI} < 30 [1m]", M1,
         lambda f: cmp(f, RSI, ">", 70) | (cmp(f, "close", ">", "sma(20)") & cmp(f, RSI, "<", 30)),
         note="a OR (b AND c): AND binds tighter than OR; a flat left-to-right fold would give (a OR b) AND c"),
    Case("or-and-or-precedence", f"{RSI} < 30 OR close > sma(20) AND {RSI} > 50 OR {RSI} > 70 [1m]", M1,
         lambda f: cmp(f, RSI, "<", 30) | (cmp(f, "close", ">", "sma(20)") & cmp(f, RSI, ">", 50)) | cmp(f, RSI, ">", 70),
         note="a OR (b AND c) OR d"),
    Case("and-or-grouped", f"close > sma(20) AND ({RSI} < 30 OR {RSI} > 70) [1m]", M1,
         lambda f: cmp(f, "close", ">", "sma(20)") & (cmp(f, RSI, "<", 30) | cmp(f, RSI, ">", 70))),
    Case("or-and-grouped-left", f"({RSI} < 30 OR {RSI} > 70) AND close > sma(20) [1m]", M1,
         lambda f: (cmp(f, RSI, "<", 30) | cmp(f, RSI, ">", 70)) & cmp(f, "close", ">", "sma(20)")),
    Case("or-and-grouped-right", f"{RSI} < 30 OR (close > sma(20) AND {RSI} > 70) [1m]", M1,
         lambda f: cmp(f, RSI, "<", 30) | (cmp(f, "close", ">", "sma(20)") & cmp(f, RSI, ">", 70))),
    Case("nested-groups", f"(close > sma(20) AND (slope(close,5) > 0 OR {RSI} > 70)) OR {RSI} < 30 [1m]", M1,
         lambda f: (cmp(f, "close", ">", "sma(20)") & (cmp(f, "slope(close,5)", ">", 0) | cmp(f, RSI, ">", 70))) | cmp(f, RSI, "<", 30)),
    Case("not-grouped", f"NOT ({RSI} < 30 OR {RSI} > 70) [1m]", M1,
         lambda f: ~(cmp(f, RSI, "<", 30) | cmp(f, RSI, ">", 70))),
    Case("not-then-and", f"NOT close > sma(20) AND {RSI} < 50 [1m]", M1,
         lambda f: ~cmp(f, "close", ">", "sma(20)") & cmp(f, RSI, "<", 50),
         note="NOT binds to the comparison: (NOT a) AND b"),
    Case("group-with-range", "(close > sma(20) OR close > sma(50)) AND rsi(14,70,30,wilders) > 50 [1m, 3]", M1,
         lambda f: (cmp(f, "close", ">", "sma(20)", r=3) | cmp(f, "close", ">", "sma(50)", r=3)) & cmp(f, RSI, ">", 50, r=3),
         note="the [tf, r] suffix applies to every comparison inside the group"),
    # --- crosses
    Case("cross-over-close-sma20", "crosses_over(close, sma(20)) [1m]", M1, lambda f: crosses(f, "close", "sma(20)")),
    Case("cross-under-close-sma20-r5", "crosses_under(close, sma(20)) [1m, 5]", M1, lambda f: crosses(f, "close", "sma(20)", r=5, over=False)),
    Case("cross-over-ema5-ema20", "crosses_over(ema(5), ema(20)) [1m]", M1, lambda f: crosses(f, "ema(5)", "ema(20)")),
    # --- time gate (DST fixture: 2025-03-09 changes the UTC offset mid-fixture; half-day fixture)
    Case("time-first-30min", "time >= 570 AND time < 600 [1m]", ["TSLA_1m_2025-03-07_2025-03-11", "SPY_1m_2024-11-27_2024-12-02"],
         lambda f: cmp(f, "time", ">=", 570) & cmp(f, "time", "<", 600)),
    Case("time-and-rsi", f"time > 600 AND {RSI} < 30 [1m]", ["TSLA_1m_2025-03-07_2025-03-11"],
         lambda f: cmp(f, "time", ">", 600) & cmp(f, RSI, "<", 30)),
    # --- daily / hourly timeframes
    Case("close-gt-sma200-1d", "close > sma(200) [1d]", D1, lambda f: cmp(f, "close", ">", "sma(200)")),
    Case("rsi-oversold-1d", f"{RSI} < 30 [1d]", D1, lambda f: cmp(f, RSI, "<", 30)),
    Case("golden-cross-1d-r5", "crosses_over(sma(50), sma(200)) [1d, 5]", D1, lambda f: crosses(f, "sma(50)", "sma(200)", r=5)),
    Case("volume-gt-adv30-1d", "volume > adv() [1d]", D1, lambda f: cmp(f, "volume", ">", "adv()")),
    Case("close-gt-sma20-1h", "close > sma(20) [1h]", H1, lambda f: cmp(f, "close", ">", "sma(20)")),
    Case("rsi-overbought-1h-r2", f"{RSI} > 70 [1h, 2]", H1, lambda f: cmp(f, RSI, ">", 70, r=2)),
    # --- snapshot only (no independent reference): blessed by the C# test with GOLDEN_UPDATE=1
    # (support_resistance is non-incremental and O(bars*lookback); a 1m replay takes minutes, so use the daily fixtures)
    Case("sr-near-support-1d", "support_resistance().near_support > 0 [1d]", D1, kind="snapshot"),
    Case("sr-close-above-support-1d", "close > support_resistance().support [1d]", D1, kind="snapshot"),
]


# --------------------------------------------------------------------------------------- main

def git_sha() -> str:
    try:
        return subprocess.check_output(["git", "rev-parse", "--short", "HEAD"], cwd=REPO, text=True).strip()
    except Exception:
        return "unknown"


def main() -> None:
    previous = json.loads(OUT.read_text(encoding="utf-8")) if OUT.exists() else {"cases": []}
    prev_by_id = {c["id"]: c for c in previous.get("cases", [])}

    fixtures: dict[str, Fixture] = {}

    def fx(name: str) -> Fixture:
        if name not in fixtures:
            fixtures[name] = Fixture(name)
        return fixtures[name]

    ids = [c.id for c in CASES]
    assert len(ids) == len(set(ids)), "duplicate case id"

    out_cases = []
    for c in CASES:
        entry = {"id": c.id, "script": c.script, "kind": c.kind, "knownBug": c.known_bug, "note": c.note,
                 "evaluatedCount": {}, "expected": {}}
        for name in c.fixtures:
            f = fx(name)
            idx = f.evaluated_indices()
            entry["evaluatedCount"][name] = len(idx)
            if c.kind == "reference":
                mask = c.pred(f)
                entry["expected"][name] = [int(f.t[i]) for i in idx if mask[i]]
            else:
                entry["expected"][name] = (prev_by_id.get(c.id, {}).get("expected") or {}).get(name)
        out_cases.append(entry)
        summary = ", ".join(f"{k.split('_')[0]}:{len(v) if v is not None else '?'}/{entry['evaluatedCount'][k]}" for k, v in entry["expected"].items())
        print(f"{c.id:32s} {summary}")

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps({"generatedBy": f"tools/golden/compute_outcomes.py@{git_sha()}", "cases": out_cases},
                              indent=1, separators=(",", ":")) + "\n", encoding="utf-8")
    print(f"wrote {OUT.relative_to(REPO)} ({OUT.stat().st_size/1024:.0f} KB)")


if __name__ == "__main__":
    main()
