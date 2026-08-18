---
name: close
kind: keyword
signature: close
contexts: [scan, backtest]
since: 2025-06-01
summary: Closing price series.
---

# close

`close` is the closing price of each candle in the active timeframe. It is the most common input in
filters — compared against a level (`close > 100`), against an indicator (`close > sma(20)`), or fed
into a transform (`slope(close, 5)`). Most indicators (`sma`, `ema`, `rsi`, `macd`) are computed from
`close` internally.

## Signature & parameters

Bare keyword, no parentheses and no parameters.

Fields (dot access): `value` (default) — `close` and `close.value` are equivalent.

## Behaviour

- Per-bar series: one value per candle, oldest to newest, for the timeframe in effect (`[1m]` unless a
  `[tf]` suffix or the filter's default timeframe says otherwise). Aggregated timeframes (`[5m]`, `[1d]`)
  use the last 1-minute close inside each aggregated candle.
- The currently forming candle is included as the last point; its `close` is the latest trade price
  and changes on every tick until the bar completes.
- No warm-up: a value exists for every bar loaded. An empty bar set yields an empty series and every
  comparison against it is false.

## Where it can be used

- Scan — yes.
- Backtest — yes (entry filters and exit conditions).
- Chart — no; price is already the chart's primary series, so there is nothing to overlay.

## Examples

```
close > sma(20) [5m, 3]
```
Price closed above its 20-bar SMA on each of the last three 5-minute candles.

```
close > 5 AND close < 50
```
Price between 5 and 50 on the latest 1-minute bar.

```
crosses_over(close, vwap())
```
Price just crossed above session VWAP on the latest bar.

## Gotchas

- The last point is the forming candle, so `close` on an intraday timeframe is really "last price";
  use `[, 2]` if you want to require the condition on the previous, completed candle as well.
- Intraday bars include extended hours; `close [1d]` is the daily aggregate as delivered by the data
  provider, and while the session is open it is the forming daily candle (i.e. last price).
- Comparing two series (`close > open`) pairs bars by position from the newest end; both series must be
  in the same timeframe for the pairing to make sense.

## Changelog

- 2025-06-01 — added.
