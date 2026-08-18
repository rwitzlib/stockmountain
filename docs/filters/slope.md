---
name: slope
kind: transform
signature: slope(series[, period])
contexts: [scan, backtest]
since: 2025-06-01
summary: Least-squares (linear regression) slope of any series over a rolling window, default 5 bars.
---

# slope(series[, period])

`slope` measures how fast a series is rising or falling by fitting a straight line through the last
`period` values and returning that line's gradient, in price (or indicator) units per bar. Traders
reach for it to turn "is the 20 EMA turning up?" or "is the MACD histogram still expanding?" into a
number that can be compared with `0` or with another slope, instead of eyeballing a chart.

## Signature & parameters

| parameter | type | required | default | meaning |
|---|---|---|---|---|
| series | series | yes | — | any series expression: `close`, `sma(20)`, `macd(12,26,9,ema).histogram`, another `slope(...)`, ... |
| period | int | no | 5 | window length in bars; must be `>= 2` |

Fields (dot access): none (single value). `slope(...)` returns a plain numeric series.

## Formula / algorithm

Input is the `value` field of the argument series (for `close`/`open`/`high`/`low`/`vwap` that is the
price itself; for an indicator it is its default field, so `slope(macd(12,26,9,ema))` uses the MACD
line — pass `.histogram` etc. explicitly for other fields).

For a window of `N = period` consecutive values `y[0..N-1]` (oldest first) with `x = 0, 1, ..., N-1`:

```
sumX  = N(N-1)/2
sumX2 = (N-1)·N·(2N-1)/6
sumY  = Σ y[i]
sumXY = Σ i · y[i]
slope = (N·sumXY − sumX·sumY) / (N·sumX2 − sumX²)
```

This is the ordinary least-squares slope of `y` against bar index; units are "value per bar". Positive
means the fitted line rises left-to-right. The output at bar `k` is the slope of the window ending at
`k` (indices `k-N+1 .. k`). No rounding is applied. Python reference: `slope()` in
`tools/golden/compute_reference.py`.

Edge cases: `period < 2` is an error; if the input series has fewer than `period` values the result
is empty; if the argument is not a series it is an error. Windows containing NaN inputs are undefined
(the C# input series never contain NaN; the Python reference skips them).

## Warm-up & seeding

- First value is emitted at index `period - 1` (0-based) of the *input series* — i.e. once `period`
  input values exist. If the input is itself an indicator with its own warm-up (e.g. `sma(20)`),
  those warm-ups add: `slope(sma(20), 10)` first exists at bar 28.
- No seeding: every value is a full least-squares fit of a complete window.
- Before that nothing is emitted — bars are omitted, not zeroed. Comparisons against a missing value
  are false.

## Forming bar & timeframes

- On the currently forming candle the last window includes the forming bar's provisional value and is
  recomputed on every tick (`IIncrementalSeriesFunction`; the last cached point is always
  recomputed, earlier points are reused).
- `[5m]`, `[1d]` etc. select the bar size the input series is built on; `period` is then measured in
  those bars. `slope(close, 5) [1d]` is the 5-day regression slope of daily closes.
- Warm-up need = warm-up of the input series + `period - 1` bars. `slope(sma(200), 5) [1d]` needs a
  full year of dailies just like `sma(200) [1d]`.
- Sessions do not reset the window: on intraday timeframes the window spans the overnight gap.

## Where it can be used

Scan and backtest. Not chartable: it is a generic transform whose scale depends entirely on what is
fed in, so it is not exposed as a `POST /stocks` study. Use it inside filter expressions only.

## Examples

```
slope(close, 5) > 0 [5m]
```
Price has been rising, on a least-squares basis, over the last five 5-minute bars.

```
slope(sma(20), 10) > 0 AND rsi(14,70,30,wilders) > 50 [1m]
```
The 20-bar SMA is turning up and momentum is confirming.

```
slope(ema(20), 5) > slope(ema(50), 5) [1d]
```
Fast average is steepening relative to the slow one — trend acceleration.

```
slope(macd(12,26,9,ema).histogram, 3) > 0 [15m, 2]
```
MACD histogram expanding for the last two 15-minute bars.

## Gotchas

- Units are *value per bar*, not percent. `slope(close, 5) > 0.5` means something very different for a
  $5 stock and a $500 stock. Compare against `0`, against another slope of the same series, or divide by
  price if you need a scale-free number.
- Default period is 5, so `slope(close)` and `slope(close, 5)` are identical.
- `slope(indicator)` uses the indicator's default `value` field; specify `.histogram`, `.signal`,
  `.upper` etc. if you mean another field.
- Very short periods (2–3) are noisy; `slope(x, 2)` is just `x[t] − x[t-1]`.
- The last value moves with the forming bar; use a candle range like `[5m, 2]` if you want a
  confirmed reading.

## Changelog

- 2025-06-01 — added.
- 2026-08-17 — incremental `Append` made O(period) per bar (was O(n)); last cached point always
  recomputed so the forming bar can never leave a stale slope.
