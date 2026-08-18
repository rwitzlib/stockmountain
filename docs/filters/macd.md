---
name: macd
kind: series
signature: macd(fast, slow, signal, type)
contexts: [scan, backtest, chart]
since: 2025-06-01
summary: MACD line, signal line and histogram from fast/slow moving averages of close.
---

# macd(fast, slow, signal, type)

MACD is the difference between a fast and a slow moving average of close (the MACD line), smoothed once
more into a signal line; the histogram is their gap. Traders use histogram sign for momentum, and MACD /
signal crossovers for entries and exits.

## Signature & parameters

| parameter | type | required | default | meaning |
|---|---|---|---|---|
| fast | int | yes | — | fast average period (`>= 1`, must not exceed `slow`) |
| slow | int | yes | — | slow average period (`>= 1`) |
| signal | int | yes | — | signal average period over the MACD line (`>= 1`) |
| type | keyword | yes | — | average type for all three: `ema`, `sma` or `wilders` |

Fields (dot access): `value` (default) = MACD line, `macd` (alias of `value`), `signal`, `histogram`.

## Formula / algorithm

Input: `close[0] … close[N-1]` of the active timeframe. Let `MA(x, n)` be the chosen average with the
same convention as `ema(n)`: seed = plain mean of the first `n` values at index `n-1`, then
`prev + (x - prev) * alpha` with `alpha = 2/(n+1)` (`ema`) or `alpha = 1/n` (`wilders`); for `sma` it is
the rolling mean throughout (no seed, no recursion).

```
fastMA[i]   = MA(close, fast)        valid from i = fast-1
slowMA[i]   = MA(close, slow)        valid from i = slow-1
line[i]     = fastMA[i] - slowMA[i]                     (i >= slow-1)
signal[i]   = MA(line[slow-1 ..], signal)               # seeded from the FIRST `signal` MACD values
histogram   = line - signal
```

The signal seed is the mean of the first `signal` MACD values (indexes `slow-1 … slow+signal-2`) and
its recurrence runs over MACD values, not closes. All three fields are emitted only from the bar on
which the signal exists. Double precision, no rounding. `barCount <= slow+signal-2` → empty series.

## Warm-up & seeding

- First point (for `.value`, `.signal` and `.histogram` alike) is at bar index `slow + signal - 2`
  (0-based); e.g. `macd(12,26,9,ema)` starts at index 33, i.e. after 34 bars.
- Fast/slow are SMA-seeded at `fast-1` / `slow-1`; the signal is SMA-seeded from the first `signal`
  MACD values.
- Nothing is emitted before that — no placeholder zeros. (Older versions emitted `signal = 0` during
  warm-up, which made `value > signal` and `crosses_over(...)` fire spuriously; fixed in plan 14.)
- Same start bar as TA-Lib.

## Forming bar & timeframes

- The forming candle contributes `close[N-1]`; its point is recomputed on every tick via
  `IIncrementalSeriesFunction`, continuing fast/slow/signal from the last completed cached point.
- `[5m]`, `[1h]`, `[1d]` select which candles feed the averages.
- Backtest warm-up (`DataCache`) should provide well over `slow + signal` bars (recursive types
  converge on history — aim for 3× that); `macd(12,26,9,ema) [1d]` wants 100+ daily bars.

## Where it can be used

- Scan — yes.
- Backtest — yes.
- Chart — yes; drawn in its own pane (line, signal, histogram).

## Examples

```
macd(12,26,9,ema).histogram > 0 [5m, 3]
```
Bullish momentum on the 5-minute chart in any of the last three candles.

```
crosses_over(macd(12,26,9,ema).value, macd(12,26,9,ema).signal) [1d]
```
Daily MACD line just crossed above its signal line.

```
macd(12,26,9,ema) > 0 AND macd(12,26,9,ema).histogram > 0 [15m]
```
MACD above zero and above its signal — trend and momentum aligned.

## Gotchas

- `.value` and `.macd` are the same field; a bare `macd(...) > 0` compares the MACD line.
- Standard `type` is `ema`; `sma`/`wilders` apply the same average kind to fast, slow **and** signal.
- Nothing is emitted for the first `slow + signal - 1` bars, so a filter that needs 34+ bars on `[1d]`
  is silently false on tickers with less history.
- Recursive types (`ema`, `wilders`) depend on how much history is loaded — small differences from a
  long-running chart in the first ~100 bars.
- `fast > slow` is rejected at validation.

## Changelog

- 2025-06-01 — added.
- 2026-08-16 — warm-up placeholder zeros removed; first point at `slow + signal - 2` (plan 14).
- 2026-08-17 — incremental recompute of the forming-bar point.
