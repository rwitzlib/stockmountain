---
name: low
kind: keyword
signature: low
contexts: [scan, backtest]
since: 2025-06-01
summary: Low price series.
---

# low

`low` is the lowest traded price inside each candle of the active timeframe. It is used for support
and breakdown levels (`low < 95`), price-range filters (`low > 100 AND high < 200`), and — via `[1d]` —
the day's low for "holding above the low of day" logic.

## Signature & parameters

Bare keyword, no parentheses and no parameters.

Fields (dot access): `value` (default) — `low` and `low.value` are equivalent.

## Behaviour

- Per-bar series: one value per candle, oldest to newest, for the timeframe in effect (default `[1m]`).
  Aggregated timeframes take the minimum of the 1-minute lows inside each aggregated candle.
- The forming candle is included as the last point; its `low` can only ratchet downward until the bar
  completes.
- No warm-up: a value exists for every bar loaded. An empty bar set yields an empty series and every
  comparison against it is false.

## Where it can be used

- Scan — yes.
- Backtest — yes (entry filters and exit conditions).
- Chart — no; price is already the chart's primary series.

## Examples

```
low > 100 AND high < 200
```
The latest 1-minute bar traded entirely inside 100–200.

```
low > vwap() [5m, 3]
```
Each of the last three 5-minute candles held entirely above session VWAP.

```
close <= low [1m]
```
The current minute is printing at its low (selling into the close of the bar).

## Gotchas

- `low [1d]` is the forming daily candle's low so far, not yesterday's low.
- Backtests use `high`/`low` of each bar for intrabar stop and target fills; a stop can be hit on the
  same bar that satisfies a `low`-based entry filter.
- Series-vs-series comparisons pair bars by position from the newest end; keep both sides in the same
  timeframe.

## Changelog

- 2025-06-01 — added.
