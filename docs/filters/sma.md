---
name: sma
kind: series
signature: sma(period)
contexts: [scan, backtest, chart]
since: 2025-06-01
summary: Simple moving average of close over the last `period` bars.
---

# sma(period)

The simple moving average is the arithmetic mean of the last `period` closing prices. Traders use it as a
smoothed view of trend (price above/below the average), as dynamic support/resistance, and in pairs
(`sma(20) > sma(50)`) to define trend regime or detect crossovers.

## Signature & parameters

| parameter | type | required | default | meaning |
|---|---|---|---|---|
| period | int | yes | — | number of bars (including the current one) to average |

Fields (dot access): `value` (default) — `sma(20)` and `sma(20).value` are equivalent.

## Formula / algorithm

Input: the `close` of every bar in the active timeframe, oldest to newest (`close[0] … close[N-1]`).

For every bar index `i >= period - 1`:

```
sma[i] = ( close[i-period+1] + … + close[i] ) / period
```

- Plain arithmetic mean, no weighting, no rounding (double precision).
- If fewer than `period` bars are available the function returns an empty series (every comparison
  against it is false).
- `period` must be a positive integer; `period <= 0` fails validation.

Python equivalent: `pd.Series(close).rolling(period).mean()`.

## Warm-up & seeding

- First value is emitted at bar index `period - 1` (0-based) — the first bar that has a full window.
- No seeding: the first value is simply the mean of the first `period` closes.
- Nothing is emitted before that — bars are omitted, not zeroed. Comparisons on omitted bars are false.

## Forming bar & timeframes

- The currently forming candle is included as `close[N-1]` (its live last price). The point is recomputed
  on every tick via `IIncrementalSeriesFunction`; earlier points are reused from cache.
- `[5m]`, `[1h]`, `[1d]` change which candles are averaged: `sma(20) [5m]` is the mean of the last 20
  five-minute closes; `sma(200) [1d]` is the mean of the last 200 daily closes.
- The backtester's warm-up (`DataCache`) must load at least `period` bars of that timeframe before the
  first evaluated bar — `sma(200) [1d]` needs roughly a year of daily history. Sessions are not treated
  specially: intraday windows run across the overnight gap.

## Where it can be used

- Scan — yes.
- Backtest — yes (entry filters and exit conditions).
- Chart — yes; overlays on the price pane on `POST /stocks`.

## Examples

```
close > sma(20) [5m, 3]
```
Price closed above its 20-bar SMA on any of the last three 5-minute candles.

```
sma(20) > sma(50) [1d]
```
Daily 20-day average above the 50-day average — a simple up-trend regime filter.

```
crosses_over(close, sma(50)) [1m]
```
Price just crossed above the 50-bar SMA on the 1-minute chart.

## Gotchas

- Not enough history means an empty series, not zeros — a filter such as `sma(200) [1d]` silently fails
  on tickers with under 200 daily bars (recent IPOs).
- The forming candle is included, so an intraday SMA moves with every tick until the bar closes; use a
  candle-count such as `[, 2]` if you want a value that no longer changes.
- Same value as TradingView/ThinkOrSwim `SMA` on close; the only differences come from data (our bars
  are Massive/Polygon aggregates including extended hours on intraday timeframes).

## Changelog

- 2025-06-01 — added.
- 2026-08-17 — incremental (forming-bar) recompute of the last cached point (plan 14 follow-up #1).
