---
name: support_resistance
kind: series
signature: support_resistance(lookback, swing, cluster%, atrMult, atrPeriod, minTouches)
contexts: [scan, backtest, chart]
since: 2025-06-01
summary: Composite support/resistance zone mapper (alias sr) fusing rolling extremes, floor pivots, swing clusters, volume profile and anchored VWAP.
---

# support_resistance(lookback, swing, cluster%, atrMult, atrPeriod, minTouches)

`support_resistance` (alias `sr`) finds, for every bar, the strongest support zone below price and the
strongest resistance zone above it, and exposes the zone centres, widths, strengths, touch counts and
distances as fields. It fuses several classic techniques into one weighted vote — Donchian-style
rolling extremes, floor pivots (P/S1/S2/R1/R2), fractal swing highs/lows, a lightweight volume-at-price
profile and an anchored VWAP — so a trader can write "price is inside a strong support band" or
"breaking above fading resistance" without hand-rolling each level.

## Signature & parameters

All parameters are optional; pass the leading ones you care about and the rest default.

| parameter | type | required | default | meaning |
|---|---|---|---|---|
| lookback | int | no | 250 | bars scanned for levels; clamped to `>= 50` and `>= 4·swing` |
| swing | int | no | 3 | bars either side needed to confirm a swing high/low pivot; `>= 1` |
| cluster% | double | no | 0.8 | clustering tolerance as % of price (0.8 ⇒ 0.8%); `>= 0.05` |
| atrMult | double | no | 0.75 | ATR multiple used as an alternative clustering tolerance; `>= 0.1` |
| atrPeriod | int | no | 14 | ATR lookback (simple average of true range); `>= 5` |
| minTouches | int | no | 2 | touches a zone needs for the full touch bonus; `>= 1` |

Fields (dot access): `value` (default; `resistance_distance_pct − support_distance_pct`, positive =
closer to resistance), `support`, `resistance` (zone centres), `support_strength`,
`resistance_strength` (0–1), `support_distance`, `resistance_distance` (absolute, ≥ 0),
`support_distance_pct`, `resistance_distance_pct` (% of close), `support_zone_width`,
`resistance_zone_width`, `support_touches`, `resistance_touches`, `support_upper`, `support_lower`,
`resistance_upper`, `resistance_lower` (centre ± width/2), `near_support`, `near_resistance` (1 when
close is inside the band, else 0). Missing zone ⇒ NaN for prices/distances, 0 for strength/touches.

## Formula / algorithm

Per bar `i`, with window `[ws, i]`, `ws = max(0, i − lookback + 1)`:

1. **ATR**: `tr[0] = high−low`; `tr[k] = max(h−l, |h−prevClose|, |l−prevClose|)`;
   `atr[k]` = simple mean of the last `atrPeriod` TRs (fewer while warming up).
2. **Tolerance** `tol = max(|close|·cluster%/100, atr[i]·atrMult, (winHigh−winLow)·0.01, 0.01)`.
3. **Candidates** (price, weight, width) go into a support list or a resistance list:
   - Rolling extremes: `winLow` → support, `winHigh` → resistance; weight 0.75,
     width `max(tol, 0.25·(winHigh−winLow))`.
   - Floor pivots from bars `[max(ws, i−max(10, 3·swing)), i−1]`: `P=(H+L+C)/3`,
     `S1=2P−H`, `S2=P−(H−L)`, `R1=2P−L`, `R2=P+(H−L)`. S1/R1 weight 0.9, S2/R2 0.75, `P` 0.8 on the
     side of close it lies; widths `1.2·tol` (×1.1 for S2/R2). Skipped if fewer than 3 bars.
   - Swing pivots for `k` in `[ws+swing, i−swing]`: low pivot if every bar within `swing` to the
     left has `low > bar.low` and to the right `low >= bar.low` (mirror for highs). Weight
     `1 + recency + 0.25·volRatio + roundNumberBoost + confluenceBoost` where
     `recency = clamp(1 − (i−k)/lookback, 0.05, 1)`, `volRatio = clamp(vol/avgVol, 0.2, 4)`,
     round-number boost 0.15/0.10/0.10 for being within `max(0.05, 0.2%)` of a whole / ×5 / ×10 price,
     confluence boost up to 0.6 for lying within `1.5·tol` of winHigh/winLow/midpoint/P/S1/S2/R1/R2.
     Width `max(atr[k], bar range, tol, 0.5·|close|·cluster%/100)`.
   - Volume profile: bucket typical price `(H+L+C)/3` by `max(0.75·tol, 0.25%·|close|)`, take the 4
     highest-volume buckets, volume-weighted mean price, weight `0.6 + clamp(bucketVol/(avgVol·winLen), 0.4, 3.5)`,
     width `max(tol, 1.5·bucket)`; support if `<= close`, else resistance.
   - Anchored VWAP over the window (typical price × volume), weight 0.8, width `max(tol, 0.2%·|close|)`.
4. **Zones**: candidates sorted by weight desc are merged into the nearest existing zone whose centre
   is within `max(tol, zone.width)`, else start a new zone. Merge: `weightSum += w`, `touches += 1`,
   centre = weight-average, width = max. Zone `strength = weightSum · touchBonus · (0.5 + recency)`
   with `touchBonus = 1 + 0.25·(touches − minTouches + 1)` if `touches >= minTouches` else 0.75, and
   `recency = clamp(1 − (i − lastIdx)/winLen, 0.05, 1)`.
5. **Broken penalty**: strength ×0.6 if any close in the last `clamp(i−ws, 5, 20)` bars closed beyond
   `centre ∓ width/2` on the wrong side.
6. **Pick**: `score = strength · (0.7 + 1/(1+|close−centre|)) · orientation`, orientation 1.0 if the
   zone is on its own side of price (within `0.25·width` slack) else 0.65; highest score wins.
7. **Output**: distances clamp at 0; `strength_out = 1 − exp(−strength/3)`.

Reference: the C# in `SupportResistanceFunction.cs` *is* the spec — see Gotchas.

## Warm-up & seeding

- Fewer than 10 bars in total ⇒ empty series.
- Otherwise the first value is emitted at index `2·swing + 4` (0-based) — bar 10 with the default
  `swing = 3` — because a window shorter than `2·swing + 5` bars is skipped.
- There is no seeding; every point is a full recomputation over its own window. Levels are simply
  less informed until `lookback` bars are available.
- Bars before the first value are omitted, not zeroed.

## Forming bar & timeframes

- Not incremental: this function does **not** implement `IIncrementalSeriesFunction`. Every tick on
  the forming candle triggers a full recompute of the whole series (`Cost = 6`, the most expensive
  function in the registry). Prefer it in scans with a coarse timeframe or a small universe, and avoid
  stacking many `sr()` calls with different parameters in one filter.
- The forming bar participates as `bars[i]` for the last point (its close, high, low, volume), so
  `near_support`/`value` update intra-bar. It cannot be a swing pivot until `swing` more bars exist.
- `[5m]`, `[1d]` set the bar size; `lookback` and `swing` count those bars. `sr() [1d]` needs ~250
  daily bars (a year) for a full window; the backtester's `DataCache` warm-up must cover that.
- Sessions do not reset the window on intraday timeframes; the anchored VWAP is anchored at the
  window start, not the session open.

## Where it can be used

Scan, backtest and chart (`POST /stocks` renders the zones as a study). Alias `sr` works everywhere
`support_resistance` does.

## Examples

```
support_resistance().near_support = 1 AND support_resistance().support_strength > 0.65 [1d]
```
Price sits inside a strong daily support band.

```
sr(120, 2, 0.6).support_touches >= 3 AND sr(120, 2, 0.6).support_zone_width < 1.5 [5m]
```
Intraday: a tight support zone that has been respected at least three times.

```
sr().near_resistance = 1 AND sr().resistance_strength < 0.4 AND close > sr().resistance_upper [15m, 2]
```
Breakout above weak resistance, confirmed for two bars.

```
close > support_resistance().support AND sr().value < 0 [1d]
```
Above support and closer to support than to resistance.

## Gotchas

- **No independent oracle.** Unlike `sma`, `rsi`, `slope` etc., support_resistance is not backed by a
  Python reference implementation; the golden tests are *snapshot* tests (`sr-near-support-1d`,
  `sr-close-above-support-1d`), so they detect unintended change but do not prove correctness. Treat
  the algorithm above as documentation of current behaviour rather than a validated formula.
- Parameters are positional and leading-only: to change `atrMult` you must also pass `lookback`,
  `swing` and `cluster%`. Out-of-range values are clamped silently, never rejected.
- `lookback` is silently raised to at least 50 (and 4·swing); `sr(20)` behaves as `sr(50)`.
- Full recompute per evaluation — heavy in live scans across a large universe.
- `value` is a signed *percent* difference (`resistance_distance_pct − support_distance_pct`), not a
  price. Use `support`/`resistance` fields when you need levels.
- If no zone exists on one side, `support`/`resistance` and distances are NaN and comparisons
  against them are false; strengths and touches read 0.
- Zone picking favours proximity as well as strength, so the reported level can jump between bars
  when two zones score similarly. Add a strength or touches threshold to stabilise signals.
- Different parameters produce different zone sets; comparing `sr(120,2,0.6).support` with
  `sr().support_zone_width` in one filter mixes two unrelated computations.

## Changelog

- 2025-06-01 — added.
- 2026-08-16 — covered by snapshot golden tests on daily AAPL/NVDA fixtures.
