---
title: Filter reference
summary: The filter expression language used by scans, backtests and live strategies — operators, the timeframe/candle suffix, and every keyword and function.
---

# Filter reference

Filters are short expressions that decide whether a ticker matches, evaluated on the ticker's bar data
at a point in time. The same language is used by scans (evaluated on the live clock), backtests
(replayed minute by minute) and live strategies, so a filter that matches in a backtest matches the
same way live.

## What a filter is

- A strategy or scan holds a list of filters, **one expression per line**.
- Every line must be true for the ticker to match — the lines are implicitly `AND`ed.
- Each line is a boolean expression: comparisons joined by logical operators, optionally followed by a
  `[timeframe, candles, mode]` suffix that says which candles the comparisons look at.

```
close > sma(20) [5m, 3]
volume > adv() [5m]
time >= 9:45 AND time < 11:00
float < 50000000
```

## Comparison operators

`>`  `<`  `>=`  `<=`  `=`  `!=`

- Both sides may be a number, a keyword, a function call or a dot-field. Series-vs-scalar
  (`close > 100`), scalar-vs-series (`100 < close`) and series-vs-series (`close > sma(20)`,
  `sma(20) > sma(50)`, `close > macd(12,26,9,ema).signal`) are all allowed.
- Series-vs-series pairs bars by position from the newest end, so both sides should be in the same
  timeframe. Indicators with different warm-up lengths are aligned automatically.
- `=` and `!=` use an epsilon of 1e-9 for floating-point equality.
- An empty series (not enough history) or a `NaN` scalar (e.g. missing `float`) makes every comparison
  false.

## Logical operators

- `AND`, `OR`, `NOT` — case-insensitive.
- `AND` binds tighter than `OR`: `a OR b AND c` is `a OR (b AND c)`.
- `NOT` applies to the next comparison, function call or parenthesised group: `NOT close > 100`,
  `NOT (a AND b)`.
- Parentheses group: `(rsi(14,70,30,wilders) < 30 OR close < sma(200)) AND volume > adv()`.
- Boolean functions (`crosses_over`, `crosses_under`) stand alone as terms: `crosses_over(close, sma(20)) AND time < 10:30`.

## The `[timeframe, candles, mode]` suffix

One optional bracket suffix at the **end of the line**; it applies to every comparison on that line.
Parts are comma-separated and may appear in any order; each is optional.

| suffix | meaning |
|---|---|
| *(none)* | default timeframe (`1m`), latest candle only |
| `[5m]` | evaluate on 5-minute candles, latest candle only |
| `[5m, 3]` | 5-minute candles; the comparison must hold on **all** of the last 3 candles |
| `[5m, 3, any]` | 5-minute candles; must hold on **any** of the last 3 candles |
| `[, 2]` | default timeframe, all of the last 2 candles |
| `[1d]` | daily candles, latest (forming) daily candle |

- Timeframes: a quantity plus a unit — `1m`, `5m`, `15m`, `30m`, `1h`, `4h`, `1d`, `1w`, `1mo`.
  Units accept `m/min/minute`, `h/hr/hour`, `d/day`, `w/wk/week`, `mo/month`; a bare unit means 1
  (`[h]` = `[1h]`). Default is `1m`.
- Candles: a positive integer; the last N candles including the currently forming one. If fewer than
  N candles exist, only the available ones are checked (an empty series is false).
- Mode: `all` (default) or `any`.
- The last candle in every timeframe is the **forming** candle: its `close` is the last price, its
  `volume` is volume so far, and indicators are recomputed on it every tick.
- Scalars (`float`) and the evaluation clock (`time`) are single values; the suffix does not change
  them.

## Dot field access

Multi-field results expose named fields with a dot: `macd(12,26,9,ema).signal`,
`macd(12,26,9,ema).histogram`, `support_resistance().support_strength`, `time.hour`. Without a field
the default `value` is used — `sma(20)` and `sma(20).value` are identical. Field names are
case-insensitive.

## Time literals

`H:MM` / `HH:MM` (24-hour, Eastern) is a literal meaning minutes since midnight, for use with `time`:
`time < 10:30`, `time >= 9:45 AND time < 11:00`. Minutes must be two digits; hours 0–23.

## Keywords and functions

| name | signature | kind | summary |
|---|---|---|---|
| [close](./close) | `close` | keyword | Closing price series |
| [open](./open) | `open` | keyword | Opening price series |
| [high](./high) | `high` | keyword | High price series |
| [low](./low) | `low` | keyword | Low price series |
| [volume](./volume) | `volume` | keyword | Volume series (per bar; the forming bar's volume so far) |
| [float](./float) | `float` | keyword | Ticker share float (scalar; fails comparison when unavailable) |
| [time](./time) | `time` | keyword | Evaluation time of day, Eastern. Compare against HH:MM, or use .hour / .minute |
| [sma](./sma) | `sma(period)` | series | Simple moving average of close over the last `period` bars |
| [ema](./ema) | `ema(period)` | series | Exponential moving average of close (SMA-seeded at bar `period`, alpha = 2/(period+1)) |
| [macd](./macd) | `macd(fast, slow, signal, type)` | series | MACD; fields: value, signal, histogram. First point after slow+signal-1 bars of warm-up (no placeholder zeros). Type: ema / sma / wilders |
| [rsi](./rsi) | `rsi(period, overbought, oversold, type)` | series | Relative Strength Index (0–100). All 4 args required; overbought/oversold are informational only. Type: wilders / ema / sma |
| [adv](./adv) | `adv([period])` | series | Average volume over the last `period` bars of the active timeframe including the current bar (default 30); classic ADV on [1d] |
| [vwap](./vwap) | `vwap([anchor])` | series | Session VWAP anchored at 09:30 ET (no value pre-market); vwap(day) anchors at the Eastern date change to include pre-market |
| [slope](./slope) | `slope(series[, period])` | transform | Linear regression slope of a series over a rolling window (default 5) |
| [crosses_over](./crosses_over) | `crosses_over(series1, series2)` | boolean | True when series1 crosses above series2 on the latest bar |
| [crosses_under](./crosses_under) | `crosses_under(series1, series2)` | boolean | True when series1 crosses below series2 on the latest bar |
| [support_resistance](./support_resistance) | `support_resistance(lookback, swing, cluster%, atrMult, atrPeriod, minTouches)` | series | Support/resistance zone mapper (alias: `sr`). Positive value = closer to resistance |

Kinds: **keyword** — bare name, no parentheses; **series** — one value per bar, usable on either side
of a comparison and inside transforms; **transform** — takes a series and returns a series;
**boolean** — a complete condition on its own.
