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
close > sma(20) [5m, 3, all]
volume > adv() [5m]
time >= 9:45 AND time < 11:00 [1m]
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
The slots are positional and comma-separated: `[timeframe]`, `[timeframe, candles]` or
`[timeframe, candles, mode]`. Inside a bracket the timeframe is always required.

| suffix | meaning |
|---|---|
| *(none)* | 1-minute candles, latest candle only (a series line is saved as `[1m]`) |
| `[5m]` | evaluate on 5-minute candles, latest candle only |
| `[5m, 3]` | 5-minute candles; the comparison must hold on **all** of the last 3 candles (saved as `[5m, 3, all]`) |
| `[5m, 3, any]` | 5-minute candles; must hold on **any** of the last 3 candles |
| `[1d]` | daily candles, latest (forming) daily candle |

- Timeframes: a quantity plus a unit — `1m`, `5m`, `15m`, `30m`, `1h`, `4h`, `1d`, `1w`, `1mo`.
  Units accept `m/min/minute`, `h/hr/hour`, `d/day`, `w/wk/week`, `mo/month`; a bare unit means 1
  (`[h]` = `[1h]`). A line without a suffix runs on `1m` everywhere (scans, backtests, the chart tool).
- Candles: a positive integer; the last N candles including the currently forming one.
  - `all` (the default) needs the **full window**: if fewer than N candles, or fewer than N indicator
    values (warm-up, session-anchored `vwap()`), are available the line is false.
  - `any` is true as soon as one available candle satisfies the comparison; no candles is false.
- Mode: `all` or `any`; only allowed when candles is greater than 1.
- Crosses: `crosses_over` / `crosses_under` fire when the cross happens on **any** candle in the
  window whatever the mode says. A line made only of crosses rejects `all` and is saved with `any`.
  On a mixed line (`close > sma(20) AND crosses_over(close, vwap()) [1m, 5, all]`) `all` governs the
  comparison and the cross is any-of-window.
- Scalars: `float` is a per-ticker value, so a line with no bar data (`float < 50000000`) takes no
  suffix and is saved bare — a bare line always means a scalar filter. `time` is the evaluation
  clock; a suffix on a `time`-only line is allowed but changes nothing.
- Rejected, never silently reinterpreted: `[5]`, `[, 5]`, `[any]`, `[1m, any]`, `[1m, 1, any]`, a
  suffix on a scalar-only line, `all` on a cross-only line, more than three slots, and anything after
  the closing `]`.
- Canonical form: when you add or save a filter the app rewrites it into one spelling. `close > sma(20)`
  becomes `close > sma(20) [1m]`; `[1m, 5]` becomes `[1m, 5, all]`; `[1m, 1]` becomes `[1m]`;
  function arguments are separated by `, `. Only the text changes, never the meaning.
- The last candle in every timeframe is the **forming** candle: its `close` is the last price, its
  `volume` is volume so far, and indicators are recomputed on it every tick.

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
| [crosses_over](./crosses_over) | `crosses_over(series1, series2)` | boolean | True when series1 crosses above series2 on the latest bar; either side may be a fixed level (`crosses_over(rsi(14,70,30,wilders), 30)`) |
| [crosses_under](./crosses_under) | `crosses_under(series1, series2)` | boolean | True when series1 crosses below series2 on the latest bar; either side may be a fixed level |
| [support_resistance](./support_resistance) | `support_resistance(lookback, swing, cluster%, atrMult, atrPeriod, minTouches)` | series | Support/resistance zone mapper (alias: `sr`). Positive value = closer to resistance |

Kinds: **keyword** — bare name, no parentheses; **series** — one value per bar, usable on either side
of a comparison and inside transforms; **transform** — takes a series and returns a series;
**boolean** — a complete condition on its own.
