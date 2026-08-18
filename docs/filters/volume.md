---
name: volume
kind: keyword
signature: volume
contexts: [scan, backtest]
since: 2025-06-01
summary: Volume series (per bar; the forming bar's volume so far).
---

# volume

`volume` is the number of shares traded inside each candle of the active timeframe. On its own it
gates liquidity (`volume > 100000 [1d]`); compared with `adv()` it flags relative-volume spikes; on
`[1d]` it is the running total for the session.

## Signature & parameters

Bare keyword, no parentheses and no parameters.

Fields (dot access): `value` (default) — `volume` and `volume.value` are equivalent.

## Behaviour

- Per-bar series: one value per candle, oldest to newest, for the timeframe in effect (default `[1m]`).
  Aggregated timeframes sum the 1-minute volumes inside each aggregated candle.
- The forming candle is included as the last point and its value is the volume traded **so far** in
  that bar. Early in a bar it is small; it grows monotonically until the bar completes.
- No warm-up: a value exists for every bar loaded. An empty bar set yields an empty series and every
  comparison against it is false. Values are whole shares stored as double.

## Where it can be used

- Scan — yes.
- Backtest — yes (entry filters and exit conditions).
- Chart — no; the chart already draws volume in its own pane.

## Examples

```
volume > adv() [5m]
```
Current 5-minute candle has already traded more than the average 5-minute volume.

```
volume > 500000 [1d]
```
At least 500k shares traded so far today.

```
close > open AND volume > 50000 [1m, 3]
```
Three consecutive green minutes, each on more than 50k shares.

## Gotchas

- Because the last point is the forming bar's partial volume, `volume > X` on the newest bar is
  biased low right after the bar opens; compare on `[, 2]` (completed previous bar must also pass) or
  use `adv()` on a coarser timeframe if that matters.
- Intraday bars include pre-market and after-hours volume; `[1d]` volume includes extended hours too.
- Series-vs-series comparisons pair bars by position from the newest end; keep both sides in the same
  timeframe.

## Changelog

- 2025-06-01 — added.
- 2026-08-17 — stored as double end-to-end (previously narrowed on some paths).
