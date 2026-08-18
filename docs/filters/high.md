---
name: high
kind: keyword
signature: high
contexts: [scan, backtest]
since: 2025-06-01
summary: High price series.
---

# high

`high` is the highest traded price inside each candle of the active timeframe. Traders use it for
breakout levels (`high > 150`), range filters (`low > 100 AND high < 200`), and — via `[1d]` — the
day's high for "new high of day" style logic.

## Signature & parameters

Bare keyword, no parentheses and no parameters.

Fields (dot access): `value` (default) — `high` and `high.value` are equivalent.

## Behaviour

- Per-bar series: one value per candle, oldest to newest, for the timeframe in effect (default `[1m]`).
  Aggregated timeframes take the maximum of the 1-minute highs inside each aggregated candle.
- The forming candle is included as the last point; its `high` can only ratchet upward until the bar
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
close >= high [5m]
```
The current 5-minute candle is closing on its high (strong buying into the close).

```
high > 150 AND close > 148 [, 1]
```
Bar poked above 150 and is holding near it.

## Gotchas

- `high [1d]` moves throughout the day (it is the forming daily candle's high so far); it is not
  yesterday's high. Use `[1d, 2]` semantics or a completed timeframe when you need the prior day.
- Backtests use `high`/`low` of each bar for intrabar stop and target fills, so a filter on `high` and a
  target exit can trigger on the same bar.
- Series-vs-series comparisons pair bars by position from the newest end; keep both sides in the same
  timeframe.

## Changelog

- 2025-06-01 — added.
