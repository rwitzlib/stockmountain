---
name: vwap
kind: series
signature: vwap([anchor])
contexts: [scan, backtest, chart]
since: 2025-06-01
summary: Session VWAP anchored at 09:30 ET (or at the Eastern date change with vwap(day)).
---

# vwap([anchor])

Volume-weighted average price since the session opened — the average price at which shares actually
traded today. Intraday traders use it as the fair-value line: longs above VWAP, shorts below, and
reclaims/losses of VWAP as triggers.

## Signature & parameters

| parameter | type | required | default | meaning |
|---|---|---|---|---|
| anchor | keyword | no | `session` | `session` = reset at 09:30 America/New_York; `day` = reset at the Eastern date change (includes pre-market) |

Fields (dot access): none (single value) — `vwap()` and `vwap().value` are equivalent.

## Formula / algorithm

Input: for each bar of the active timeframe, its Massive `vw` (bar VWAP), `h`, `l`, `c`, `v`, timestamp.

```
price[i]  = vw[i] if vw[i] > 0 else (h[i] + l[i] + c[i]) / 3
volume[i] = max(v[i], 0)

# a bar OPENS a session key K (Eastern date) when:
#   session: its span [start, start + timeframe) ends after 09:30 ET on its date
#            (start + tf > 09:30) — the 09:00 hourly bar and the daily bar do; the 09:29 minute bar does not
#   day:     always (K = its Eastern date)
if bar opens K and K != currentKey: currentKey = K; cumPV = 0; cumV = 0
if currentKey is unset:                skip (no value)          # history starts pre-market
cumPV += price[i] * volume[i];  cumV += volume[i]
vwap[i] = cumPV / cumV   (or price[i] if cumV == 0)
```

Bars that do not open a session continue the running one — so with `session`, after-hours and the next
morning's pre-market keep accumulating into the prior session until the next 09:30 reset (weekends and
holidays included). Prices are float32 (Massive), volume double; the ratio is not rounded.

## Warm-up & seeding

- First value is emitted on the first bar that opens a session: for `session`, the first bar whose span
  covers 09:30 ET; for `day`, the very first bar. Any earlier bars are omitted (not zeroed).
- No seeding beyond the reset — the first value of a session equals that bar's `price`.

## Forming bar & timeframes

- The forming candle is re-priced on every tick via `IIncrementalSeriesFunction` from the sums that
  preceded it. Live and backtest forming candles are built with a volume-weighted `vw`; if `vw` is
  missing, typical price `(h+l+c)/3` is used.
- `[1m]`/`[5m]`: normal intraday VWAP. `[1h]`: the 09:00 hourly bar opens the session because its span
  reaches past 09:30. `[1d]`: every daily bar opens its own session, so `vwap() [1d]` is just that
  day's `vw`.
- Backtest warm-up (`DataCache`) needs the intraday bars back to the current session's 09:30 (or the
  previous session, if evaluating pre-market).

## Where it can be used

- Scan — yes.
- Backtest — yes.
- Chart — yes; drawn as an overlay on the price pane.

## Examples

```
close > vwap() [1m]
```
Price above session VWAP on the latest 1-minute bar.

```
crosses_over(close, vwap()) [5m] AND time > 09:45
```
VWAP reclaim on the 5-minute chart after the opening 15 minutes.

```
close < vwap(day) [1m, 3]
```
Below the extended-hours VWAP (anchored at midnight ET, pre-market included) in any of the last 3 bars.

## Gotchas

- Pre-market, `vwap()` is the **prior** session's VWAP carried forward, so `close > vwap()` at 08:00 ET
  means "above yesterday's session VWAP". Use `vwap(day)` if you want pre-market trades included in a
  fresh anchor.
- The bare literal `vwap` (no parentheses) is the per-bar Massive `vw` field, not the cumulative
  session VWAP — use `vwap()` for the session line.
- On `[1d]` the value equals the bar's own `vw`; on `[1h]` the 09:00 bar starts the session.
- Anchor keywords are `session` and `day` only; anything else fails validation.

## Changelog

- 2025-06-01 — added.
- 2026-08-16 — session anchoring at 09:30 ET / `vwap(day)` formalised and golden-tested (plan 14).
- 2026-08-17 — forming candles use volume-weighted `vw` live and in backtest (plan 14 follow-up #7).
