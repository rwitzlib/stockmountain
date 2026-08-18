---
name: float
kind: keyword
signature: float
contexts: [scan, backtest]
since: 2026-07-10
summary: Ticker share float (scalar; fails comparison when unavailable).
---

# float

`float` is the ticker's share float — the number of shares available for public trading — taken from
the ticker's reference details. It is a **per-ticker scalar**, not a bar series, and is the standard way
to restrict a scan to low-float names (`float < 50000000`) or to exclude them.

## Signature & parameters

Bare keyword, no parentheses and no parameters.

Fields (dot access): none (single value).

## Behaviour

- Scalar: one number per ticker (double), the same on every bar. It does not vary with timeframe or
  candle count, so a `[tf, N]` suffix on a `float` comparison has no effect on the `float` side.
- Compared against a numeric literal or another scalar directly; against a series (`float > volume`)
  it is compared to the last N points of that series like any scalar-vs-series comparison.
- When the float is not available for a ticker the value is `NaN`, and **every** comparison is false —
  `float < 50000000`, `float > 0`, `float = 0` and `float != 0` all fail. A missing float can therefore
  never let a ticker through a float gate.
- The value comes from the ticker-details snapshot loaded with the ticker, refreshed with reference
  data, not from bar data.

## Where it can be used

- Scan — yes.
- Backtest — yes; uses the float known at backtest run time (not point-in-time historical float).
- Chart — no; there is nothing to plot.

## Examples

```
float < 50000000
```
Low-float names only (under 50M shares).

```
float < 20000000 AND volume > 1000000 [1d]
```
Low float that has already traded over a million shares today.

```
NOT float < 100000000
```
Excludes tickers with float under 100M — but also excludes tickers with unknown float, because the
inner comparison is false and `NOT false` is true. Prefer `float >= 100000000` if you want unknowns out.

## Gotchas

- Unavailable float fails silently (no error) — a filter that seems to match nothing on a ticker is
  usually a missing float. ETFs and many ADRs have no float.
- `NOT` inverts the false result of a NaN comparison and lets unknown-float tickers through; write the
  positive comparison you mean instead.
- Backtests are not point-in-time: a share offering after the backtest period changes today's float and
  therefore which historical bars pass.

## Changelog

- 2026-07-10 — added.
