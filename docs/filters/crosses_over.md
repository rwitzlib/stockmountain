---
name: crosses_over
kind: boolean
signature: crosses_over(series1, series2)
contexts: [scan, backtest]
since: 2025-06-01
summary: True when series1 crosses from at-or-below to strictly above series2 on the latest bar (or within a candle range).
---

# crosses_over(series1, series2)

`crosses_over` fires on the bar where one line moves above another: price through a moving average,
a fast EMA through a slow EMA, the MACD line through its signal. It is the DSL's way of expressing an
*event* ("just crossed") rather than a *state* (`close > sma(20)`), which is what you want for entry
triggers that should fire once instead of on every bar the condition holds.

## Signature & parameters

| parameter | type | required | default | meaning |
|---|---|---|---|---|
| series1 | series | yes | — | the line doing the crossing (e.g. `close`, `ema(5)`, `macd(12,26,9,ema).value`) |
| series2 | series | yes | — | the line being crossed (e.g. `sma(20)`, `ema(20)`, `macd(12,26,9,ema).signal`) |

Fields (dot access): none (single boolean).

## Formula / algorithm

Let `a` be the `value` field of series1 and `b` of series2, aligned so both end on the same (latest)
bar. If their lengths differ the longer one is truncated from the *front* to the shorter length.
With `n` aligned values and candle range `R` (default 1):

```
crossed(j)  =  a[j-1] <= b[j-1]  AND  a[j] > b[j]
result      =  OR over j in [max(1, n-R) .. n-1] of crossed(j)
```

- Default (`[tf]` or no range): only `j = n-1` is tested — the previous bar was at-or-below and the
  latest bar is strictly above.
- With `[tf, R]`: true if a cross occurred on *any* of the last `R` bars, i.e. any of the `R`
  consecutive bar-pairs ending on the latest bar. Note the pair `(n-R-1, n-R)` needs one bar of
  history before the range.
- Equality counts as "below" beforehand and does not count as "above" afterwards, so a series that
  merely touches from below does not fire; a series that sat exactly on the other and then rises does.
- Fewer than 2 aligned values, or a non-series argument, returns false (never an error).

Python reference: `crosses(fx, left, right, r, over=True)` in `tools/golden/compute_outcomes.py`.

## Forming bar & timeframes

- The latest bar is the currently forming candle, so `crosses_over(close, sma(20))` can flicker true
  and false intra-bar as the last price wobbles around the average. Add a range (`[5m, 2]`) or wait
  for bar close in live use if you want a confirmed cross.
- `[5m]`, `[1d]` set the bar size both series are built on. Both must share the timeframe; you cannot
  cross a `[1m]` series with a `[1d]` series inside one call.
- Warm-up: the function itself only needs 2 bars, but each input needs its own warm-up.
  `crosses_over(sma(50), sma(200)) [1d, 5]` needs 200+ daily bars before it can ever fire.
- Series that start at different bars (e.g. `ema(5)` vs `ema(20)`) are aligned by their tails; the
  earliest bars where only one exists are ignored.

## Where it can be used

Scan and backtest. Not chartable — it yields a boolean, not a plottable series.

## Examples

```
crosses_over(close, sma(20)) [1m]
```
Price just closed above its 20-bar SMA on the latest 1-minute bar.

```
crosses_over(ema(5), ema(20)) [5m, 3]
```
A fast/slow EMA bull cross happened on any of the last three 5-minute bars.

```
crosses_over(sma(50), sma(200)) [1d, 5]
```
Golden cross within the last five trading days.

```
crosses_over(macd(12,26,9,ema).value, macd(12,26,9,ema).signal) AND rsi(14,70,30,wilders) < 60 [15m]
```
MACD bull cross while RSI still has room.

## Gotchas

- Argument order matters: `crosses_over(a, b)` is `a` going above `b`. `crosses_over(sma(20), close)`
  fires when *price drops through* the average (the average rises above price) — usually you want
  `crosses_under(close, sma(20))` for that.
- It is an event, not a state. `close > sma(20)` is true on every bar price sits above the average;
  `crosses_over(close, sma(20))` is true only on the bar it got there.
- The range `[tf, N]` means "any cross in the last N bars", *not* "crossed exactly N bars ago" and not
  "stayed above for N bars". Note this differs from plain comparisons, where `close > sma(20) [5m, 3]`
  requires the condition on *all* of the last 3 bars — mixing the two in one AND is rarely what you want.
- Two series that are exactly equal on both bars never fire; a series that touches from below and
  retreats never fires.
- Passing a scalar (`crosses_over(close, 100)`) is not a cross of two series and silently returns
  false. For a level break use a state comparison such as `close > 100 [1m]`.

## Changelog

- 2025-06-01 — added.
- 2026-08-16 — semantics locked by golden tests (`cross-over-close-sma20`, `cross-over-vwap-r3`,
  `golden-cross-1d-r5`) against the Python `crosses()` reference.
