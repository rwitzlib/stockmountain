---
name: rsi
kind: series
signature: rsi(period, overbought, oversold, type)
contexts: [scan, backtest, chart]
since: 2025-06-01
summary: Relative Strength Index (0–100) with Wilder, EMA or SMA smoothing of average gain/loss.
---

# rsi(period, overbought, oversold, type)

RSI measures the balance of recent gains against recent losses on a 0–100 scale. Low readings (under
30) flag an oversold stretch that mean-reversion traders buy; high readings (over 70) flag overbought.
It is the core of the "RSI low revert" strategies and is often combined with a trend filter.

## Signature & parameters

| parameter | type | required | default | meaning |
|---|---|---|---|---|
| period | int | yes | — | lookback in bars; must be `>= 2` |
| overbought | int | yes | — | informational threshold (e.g. 70) — **not used in the calculation** |
| oversold | int | yes | — | informational threshold (e.g. 30) — **not used in the calculation** |
| type | keyword | yes | — | smoothing of average gain/loss: `wilders`, `ema` or `sma` |

All four arguments are required; `rsi(14)` is a validation error. Fields (dot access): `value`
(default), `overbought`, `oversold` (the thresholds echoed back, useful on charts).

## Formula / algorithm

Input: `close[0] … close[N-1]` of the active timeframe. Let `n = period`.

```
change[i] = close[i] - close[i-1]              (i >= 1)
gain[i]   = max(change[i], 0);  loss[i] = max(-change[i], 0)

# seed (all types): plain mean of the first n gains / losses
avgGain = mean(gain[1..n]);  avgLoss = mean(loss[1..n])       -> rsi[n]

# for i > n
wilders: avgGain = (avgGain*(n-1) + gain[i]) / n               (alpha = 1/n)
ema:     avgGain = (gain[i] - avgGain) * 2/(n+1) + avgGain     (alpha = 2/(n+1))
sma:     avgGain = mean(gain[i-n+1..i])                        (rolling mean)
(avgLoss identically)

rsi[i] = 100 - 100 / (1 + avgGain/avgLoss)
```

Edge cases (epsilon 1e-12): `avgLoss ≈ 0` and `avgGain ≈ 0` (flat) → 50; `avgLoss ≈ 0` → 100;
`avgGain ≈ 0` → 0. Double precision, no rounding. Fewer than `n + 1` bars → empty series.

## Warm-up & seeding

- First value is emitted at bar index `period` (0-based) — it needs `period` price changes.
- Seeded with the simple mean of the first `period` gains and losses for all three types.
- Nothing is emitted before that; bars are omitted, not zeroed.
- `wilders`/`ema` are recursive: values on a given bar depend on how much history was loaded (see
  Gotchas). `sma` depends only on the last `period + 1` closes.

## Forming bar & timeframes

- The forming candle contributes `change[N-1] = liveClose - prevClose`; its point is recomputed on
  every tick via `IIncrementalSeriesFunction`, continuing from the last completed cached point.
- `[5m]`, `[1d]` pick the candle series; overnight gaps count as ordinary changes.
- Backtest warm-up (`DataCache`) should provide several multiples of `period` bars for the recursive
  types so the seed has decayed — `rsi(14,70,30,wilders) [1d]` wants 100+ daily bars.

## Where it can be used

- Scan — yes.
- Backtest — yes.
- Chart — yes; drawn in its own 0–100 pane with the overbought/oversold lines.

## Examples

```
rsi(14,70,30,wilders) < 30 [5m, 3]
```
Oversold on the 5-minute chart at any point in the last three candles.

```
rsi(2,90,10,wilders) < 10 [1d] AND close > sma(200) [1d]
```
Connors-style RSI(2) dip inside a daily up-trend.

```
crosses_over(rsi(14,70,30,ema), 30) [15m]
```
EMA-smoothed RSI just crossed back up through 30.

## Gotchas

- `overbought`/`oversold` are required but do not change the value — `rsi(14,70,30,wilders)` and
  `rsi(14,80,20,wilders)` are identical series. Put the threshold in the comparison.
- `wilders` is the classic RSI (ThinkOrSwim/TradingView default). Our `ema` type uses alpha 2/(n+1),
  much faster than Wilder's 1/n, so `rsi(14,70,30,ema)` will disagree with a TOS RSI(14) — a 25 vs 41
  gap on the same bar has been observed. Pick `wilders` when comparing to a broker chart.
- Recursive types converge on the loaded history; a chart with years of data and a scan with 100 bars
  differ slightly for the first ~5×period bars.
- Empty when history is shorter than `period + 1` bars, so the comparison is silently false.

## Changelog

- 2025-06-01 — added.
- 2026-08-17 — incremental recompute of the forming-bar point; golden-tested against Python reference.
