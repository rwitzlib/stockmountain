---
name: adv
kind: series
signature: adv([period])
contexts: [scan, backtest]
since: 2025-06-01
summary: Average volume over the last `period` bars of the active timeframe including the current bar (default 30).
---

# adv([period])

`adv` is the rolling average of bar volume — a simple moving average of `volume`. On the daily
timeframe it is the classic "average daily volume" used for liquidity screens; on intraday timeframes it
is average bar volume, which makes `volume > adv(20)` a relative-volume ("RVOL") style test.

## Signature & parameters

| parameter | type | required | default | meaning |
|---|---|---|---|---|
| period | int | no | 30 | number of bars (including the current one) to average; must be `>= 1` |

Fields (dot access): none (single value) — `adv()` / `adv().value` are equivalent.

## Formula / algorithm

Input: the `volume` of every bar in the active timeframe, oldest to newest (`vol[0] … vol[N-1]`).

For every bar index `i >= period - 1`:

```
adv[i] = ( vol[i-period+1] + … + vol[i] ) / period
```

- Includes the current bar `i` (the forming candle when live). There is no "exclude today" offset.
- Volume is a double (shares); the mean is not rounded.
- Fewer than `period` bars → empty series (comparisons are false).
- `adv()` with no argument is `adv(30)`; more than one argument or `period < 1` fails validation.

Python equivalent: `pd.Series(volume).rolling(period).mean()`.

## Warm-up & seeding

- First value is emitted at bar index `period - 1` (0-based).
- No seeding — the first value is the mean of the first `period` volumes.
- Nothing is emitted before that; bars are omitted, not zeroed.

## Forming bar & timeframes

- The forming candle's partial volume is `vol[N-1]`, so the live value grows during the bar. On `[1d]`
  during the session the current day's partial volume is one of the `period` terms (a 30-day ADV at
  10:00 ET is dragged down by today's incomplete bar — use `[1d, 2]`-style history or a longer period if
  that matters).
- `[1m]`, `[5m]`, `[1d]` change the unit: `adv(20) [5m]` is average 5-minute-bar volume; on intraday
  timeframes the window runs across the overnight gap and includes extended-hours bars.
- Backtest warm-up (`DataCache`) must load `period` bars of the timeframe before the first evaluated
  bar — `adv(30) [1d]` needs 30 daily bars.

## Where it can be used

- Scan — yes.
- Backtest — yes.
- Chart — no; it is a volume statistic, not drawn as a study on `POST /stocks`.

## Examples

```
volume > adv(20) [1d]
```
Today's volume (so far) exceeds the 20-day average — a daily relative-volume filter.

```
adv(30) > 1000000 [1d]
```
Liquidity screen: at least 1M shares average daily volume.

```
volume > adv(20) [5m, 1] AND close > vwap() [5m, 1]
```
Last 5-minute bar printed above its recent average bar volume while price holds above session VWAP.

## Gotchas

- Includes the current bar. On daily bars intraday, the forming day's partial volume both lowers the
  average and is the `volume` you compare against.
- It is an average of bars in the *active* timeframe, not always "daily" volume — `adv() [1m]` is a
  30-minute average bar volume, not the 30-day ADV.
- Empty when history is shorter than `period` bars (new listings), so the filter is silently false.
- Bar volume includes extended-hours trades on intraday timeframes.

## Changelog

- 2025-06-01 — added.
- 2026-08-16 — semantics fixed to a rolling mean including the current bar; golden-tested (plan 14).
