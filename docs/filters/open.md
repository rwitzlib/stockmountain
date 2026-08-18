---
name: open
kind: keyword
signature: open
contexts: [scan, backtest]
since: 2025-06-01
summary: Opening price series.
---

# open

`open` is the opening price of each candle in the active timeframe — the first trade inside the bar.
Compared with `close` it tells you the bar's direction and body (`close > open` is a green candle);
on `[1d]` it gives the day's opening print for gap and opening-range logic.

## Signature & parameters

Bare keyword, no parentheses and no parameters.

Fields (dot access): `value` (default) — `open` and `open.value` are equivalent.

## Behaviour

- Per-bar series: one value per candle, oldest to newest, for the timeframe in effect (default `[1m]`).
  Aggregated timeframes use the first 1-minute open inside each aggregated candle.
- The forming candle is included as the last point. Unlike `close`, its `open` is fixed as soon as the
  first trade of the bar prints and does not change afterwards.
- No warm-up: a value exists for every bar loaded. An empty bar set yields an empty series and every
  comparison against it is false.

## Where it can be used

- Scan — yes.
- Backtest — yes (entry filters and exit conditions).
- Chart — no; price is already the chart's primary series.

## Examples

```
close > open [5m, 3]
```
Three consecutive green 5-minute candles (each closed above its open).

```
open > close [1d]
```
Today's daily candle is red so far (opened above the current price).

```
close > open AND volume > adv() [, 1]
```
Green 1-minute bar on above-average volume.

## Gotchas

- `open [1d]` is the opening print of the daily aggregate as delivered by the data provider; if you
  want the 9:30 regular-session open of a symbol with pre-market bars, be explicit about the timeframe
  and candle you mean.
- `close > open` on the forming candle flips as price moves; combine with `[, 2]` or a completed
  timeframe if you need it to be stable.
- Series-vs-series comparisons pair bars by position from the newest end; keep both sides in the same
  timeframe.

## Changelog

- 2025-06-01 — added.
