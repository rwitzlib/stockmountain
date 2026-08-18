---
name: ema
kind: series
signature: ema(period)
contexts: [scan, backtest, chart]
since: 2025-06-01
summary: Exponential moving average of close, SMA-seeded, alpha = 2/(period+1).
---

# ema(period)

The exponential moving average weights recent closes more heavily than older ones, so it reacts faster
than an SMA of the same length. Traders use it for trend direction (`close > ema(20)`), fast/slow pairs
(`ema(9) > ema(21)`) and as the building block of MACD.

## Signature & parameters

| parameter | type | required | default | meaning |
|---|---|---|---|---|
| period | int | yes | — | smoothing length; alpha = 2 / (period + 1) |

Fields (dot access): `value` (default) — `ema(20)` and `ema(20).value` are equivalent.

## Formula / algorithm

Input: the `close` of every bar in the active timeframe, oldest to newest (`close[0] … close[N-1]`).

```
alpha            = 2 / (period + 1)
ema[period-1]    = ( close[0] + … + close[period-1] ) / period          # SMA seed
ema[i]           = (close[i] - ema[i-1]) * alpha + ema[i-1]              # for i >= period
```

- Double precision, no rounding.
- Fewer than `period` bars: empty series (comparisons are false).
- `period` must be a positive integer.

This is the TA-Lib convention (SMA seed), **not** pandas' `ewm(span=period).mean()` default, which
seeds from the first close and weights the whole history. Python equivalent:

```python
out = np.full(len(c), np.nan); a = 2/(n+1)
out[n-1] = c[:n].mean()
for i in range(n, len(c)): out[i] = (c[i]-out[i-1])*a + out[i-1]
```

## Warm-up & seeding

- First value is emitted at bar index `period - 1` (0-based).
- Seeded with the SMA of the first `period` closes; the recurrence starts at index `period`.
- Nothing is emitted before that — bars are omitted, not zeroed.
- Because the EMA is recursive, the value on a given bar depends on how much history was loaded. Values
  converge quickly (after ~3×period bars the seed's influence is under 5%), but a short history window
  gives slightly different numbers than a chart with years of data.

## Forming bar & timeframes

- The forming candle is `close[N-1]` and its point is recomputed on every tick via
  `IIncrementalSeriesFunction`; the recurrence continues from the last completed cached point.
- `[5m]`, `[1d]` select which candles feed the recurrence — `ema(20) [1d]` uses daily closes.
- Backtest warm-up (`DataCache`) should load well over `period` bars — ideally 3×`period` — so the seed
  has settled; `ema(200) [1d]` wants more than a year of daily bars.

## Where it can be used

- Scan — yes.
- Backtest — yes.
- Chart — yes; overlays on the price pane on `POST /stocks`.

## Examples

```
close > ema(20) [5m, 1]
```
Last 5-minute close is above the 20-bar EMA.

```
ema(9) > ema(21) AND close > ema(9) [1m]
```
Short-term momentum aligned: fast EMA over slow EMA and price above the fast EMA.

```
crosses_over(ema(20), ema(50)) [1d]
```
Daily 20/50 EMA golden cross on the latest bar.

## Gotchas

- Not identical to a chart that has been running for years — the SMA seed and finite lookback shift the
  first ~3×period values. Differences shrink to rounding error after that.
- Not `pandas.ewm` — if you cross-check in Python, use the SMA-seeded loop above.
- Empty (not zero) when history is shorter than `period`, so comparisons quietly return false.
- The forming bar's value moves with each tick; add a candle count (`[, 2]`) if you need it fixed.

## Changelog

- 2025-06-01 — added.
- 2026-08-17 — incremental recompute of the last cached (forming-bar) point (plan 14 follow-up #1).
