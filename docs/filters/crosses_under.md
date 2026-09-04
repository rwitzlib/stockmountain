---
name: crosses_under
kind: boolean
signature: crosses_under(series1, series2)
contexts: [scan, backtest]
since: 2025-06-01
summary: True when series1 crosses from at-or-above to strictly below series2 on the latest bar (or within a candle range).
---

# crosses_under(series1, series2)

`crosses_under` is the mirror of [`crosses_over`](./crosses_over): it fires on the bar where one line
drops below another — price losing a moving average, a fast EMA slipping under a slow one, the MACD
line falling through its signal. Use it for bearish entry triggers, for exit conditions in a long
strategy, or to detect a breakdown *event* rather than the ongoing *state* `close < sma(20)`.

## Signature & parameters

| parameter | type | required | default | meaning |
|---|---|---|---|---|
| series1 | series or number | yes | — | the line doing the crossing (e.g. `close`, `ema(5)`, `macd(12,26,9,ema).value`) |
| series2 | series or number | yes | — | the line being crossed (e.g. `sma(20)`, `ema(20)`, `macd(12,26,9,ema).signal`) or a fixed level (`30`, `0`, `100`) |

Either argument may be a plain number. A number is treated as a constant series of the same
length as the other argument, so `crosses_under(rsi(14,70,30,wilders), 70)` fires on the bar RSI
falls back through 70 and `crosses_under(30, rsi(14,70,30,wilders))` fires when RSI climbs through
30 (the constant "drops below" the rising series). Two numbers never cross and always return false.

Fields (dot access): none (single boolean).

## Formula / algorithm

Let `a` be the `value` field of series1 and `b` of series2, aligned so both end on the same (latest)
bar. If their lengths differ the longer one is truncated from the *front* to the shorter length.
With `n` aligned values and candle range `R` (default 1):

```
crossed(j)  =  a[j-1] >= b[j-1]  AND  a[j] < b[j]
result      =  OR over j in [max(1, n-R) .. n-1] of crossed(j)
```

- Default (`[tf]` or no range): only `j = n-1` is tested — the previous bar was at-or-above and the
  latest bar is strictly below.
- With `[tf, R]`: true if a cross occurred on *any* of the last `R` bars (the `R` consecutive
  bar-pairs ending on the latest bar). The first pair needs one bar of history before the range.
- Equality counts as "above" beforehand and does not count as "below" afterwards: touching from above
  and bouncing does not fire; sitting exactly on the line and then dropping does.
- A numeric argument `k` is expanded to `[k, k, …]` of the other series' length before alignment,
  so a level cross is exactly `crossed(j)` with a constant `b` (or `a`).
- Fewer than 2 aligned values, or two numeric arguments, returns false (never an error). Any other
  non-series argument type is an `ArgumentException`.

Python reference: `crosses(fx, left, right, r, over=False)` in `tools/golden/compute_outcomes.py`.

## Forming bar & timeframes

- The latest bar is the forming candle, so the result can flip intra-bar while price hovers around
  the other series. Use a range (`[5m, 2]`) or evaluate on bar close for a confirmed signal.
- `[5m]`, `[1d]` set the bar size for both inputs; both series must be on the same timeframe.
- Warm-up is that of the inputs: `crosses_under(sma(50), sma(200)) [1d, 5]` needs 200+ daily bars.
- Inputs that begin on different bars are aligned by their tails; leading bars where only one has a
  value are ignored.

## Where it can be used

Scan and backtest. Not chartable — it yields a boolean, not a plottable series.

## Examples

```
crosses_under(close, sma(20)) [1m, 5]
```
Price lost its 20-bar SMA on any of the last five 1-minute bars.

```
crosses_under(rsi(14,70,30,wilders), 70) [1m, 3]
```
RSI fell back through 70 on any of the last three minutes — the overbought run is fading.

```
crosses_under(ema(9), ema(21)) [5m]
```
Fast EMA just dropped below the slow EMA on the latest 5-minute bar.

```
crosses_under(sma(50), sma(200)) [1d, 3]
```
Death cross within the last three trading days.

```
crosses_under(macd(12,26,9,ema).value, macd(12,26,9,ema).signal) AND rsi(14,70,30,wilders) > 40 [15m]
```
MACD bear cross before RSI has become oversold.

## Gotchas

- Argument order: `crosses_under(a, b)` is `a` going below `b`. `crosses_under(sma(20), close)` is
  price *rallying through* the average, which is normally written `crosses_over(close, sma(20))`.
- Event, not state. Combine with `close < sma(20)` if you also need price to still be below.
- `[tf, N]` means "any cross in the last N bars"; a comparison such as `close < sma(20) [5m, 3]`
  means "true on all of the last 3 bars". Do not assume the two ranges compose the same way.
- Series that are equal on both bars, or that touch from above and recover, never fire.
- A level cross (`crosses_under(close, 100)`) is an *event*: true only on the bar price first closes
  below 100. For "price is below 100" (a state) use `close < 100 [1m]`. Before 2026-09-04 a numeric
  argument silently returned false — filters saved before then that use one never matched.
- On the last bar of the day at intraday timeframes, the forming 15:59 bar and the first bar of the
  next session are adjacent — a gap-down open can register as a cross on the first bar of the day.

## Changelog

- 2025-06-01 — added.
- 2026-08-16 — semantics locked by golden test `cross-under-close-sma20-r5` against the Python
  `crosses()` reference.
- 2026-09-04 — numeric arguments supported as a constant series (level crosses such as
  `crosses_under(rsi(14,70,30,wilders), 70)`); previously any number made the call silently false.
  Golden case `cross-under-rsi-level-70-r3`. Backtest entry cache bumped to v3.
