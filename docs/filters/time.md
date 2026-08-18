---
name: time
kind: keyword
signature: time
contexts: [scan, backtest]
since: 2026-07-12
summary: Evaluation time of day, Eastern. Compare against HH:MM, or use .hour / .minute.
---

# time

`time` is the time of day at which the filter is being evaluated, in America/New_York (market) time.
It is used to open and close trading windows — only take signals in the first hour, stop entering after
lunch, and so on. Compare it against `HH:MM` literals such as `time < 10:30`.

## Signature & parameters

Bare keyword, no parentheses and no parameters.

Fields (dot access): `value` (default, minutes since midnight Eastern), `hour` (0–23), `minute` (0–59).

## Behaviour

- **Evaluation clock, not a per-bar attribute.** For a live scan it is the scan's wall-clock time; for
  a backtest it is the simulated minute currently being evaluated. It is *not* the timestamp of the last
  bar — a thin ticker whose last bar is stale must not keep satisfying `time < 10:00` after 10:00. (If
  no evaluation time is supplied the last bar's timestamp is used as a fallback.)
- Value is minutes since midnight Eastern, e.g. 9:30 → 570, 16:00 → 960. `HH:MM` literals in a filter
  are parsed to the same unit, so `time >= 9:45` compares 585 with 585. Hours 0–23, minutes 0–59;
  `25:00` is a parse error.
- Single point: `time` always yields exactly one value, so the `[tf, N]` suffix has no effect on it —
  `time < 10:30 [5m, 3]` still checks the current clock once (the suffix still applies to other
  comparisons in the same filter).
- Daylight saving is handled by the time zone; the clock is Eastern local time year-round.

## Where it can be used

- Scan — yes.
- Backtest — yes; uses the simulated bar clock, so results match live behaviour.
- Chart — no; there is nothing to plot.

## Examples

```
time >= 9:45 AND time < 11:00
```
Only match between 9:45 and 11:00 Eastern.

```
close > vwap() AND time < 10:30
```
Above VWAP during the first hour of the regular session.

```
time.hour < 12
```
Morning only, using hour access (any minute before noon).

## Gotchas

- Literal must be `H:MM` or `HH:MM` with a two-digit minute: `9:30` and `09:30` work, `9:3` and
  `930` do not (`930` is a plain number and would compare against minutes since midnight).
- Because it is the evaluation clock, `time` alone cannot express "the bar at 9:30"; combine it with a
  timeframe/candle-count on the price side of the filter.
- No seconds: `time` steps once per minute. A backtest evaluated at the 15:59 bar sees `time = 15:59`.
- Always Eastern, regardless of the user's or server's local time zone.

## Changelog

- 2026-07-12 — added (per-bar time of day).
- 2026-07-21 — switched to the evaluation clock (scan time / simulated backtest minute) so stale bars cannot keep a time gate open.
