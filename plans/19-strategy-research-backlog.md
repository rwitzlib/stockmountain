# Plan 19 — Strategy research backlog

Brainstorm (2026-09-04) of strategies to backtest next, grounded in what the two best runs so far
actually did. Part A is what the data says about the current edge. Part B is strategies that can be
backtested **today** with the existing filter DSL, each with copy-pasteable filter lines. Part C is
strategies that need a capability we do not have yet (cross-referenced to plan 11 where it already
lists the gap, new items otherwise). Part D is engine/tooling work that would make every backtest
more trustworthy.

## A. What the current edge looks like

Sources: live paper strategy `b997044b175f4c83a499e6641885a5ff` ("RSI Low All Day Baby", 813 closed
trades 2026-07-24 → 2026-09-04) and backtest `d8e36f18-a025-4164-ae36-cc08a596b07d` (526 trades,
2024-01-01 → 2026-08-13). Both are "buy a 1-minute RSI(14) < 30 print, stop 2%, hold 5 minutes".

| | live paper | backtest **Hold** (timed exit) | backtest **High** |
|---|---|---|---|
| filters | `float < 10M`, `close > 1`, `volume > 10000 [1m,5]`, `rsi(14,30,70,ema) < 30 [1m]` | `rsi(14,30,70,wilders) < 30 [1m]`, `float < 50M`, `close > 2.50`, `volume > 50000 [1m,5]`, `volume > 1M [1d]`, `close > sma(200) [1d]`, `time < 14:00` | same |
| exits | stop 2% / target 15% / timed 5m | stop 2% / target 10% / timed 5m | sold at best close inside the 5m window |
| win rate | 51.3% | 43.6% | 53.0% |
| profit factor | **1.18** | **1.39** | 1.80 |
| net | +$18.6k on $100k, $10k/trade, 31 days | +$9.4k on $10k, $5k/trade | +$18.3k |
| 2026 only | — | PF **1.02** (flat) | PF 1.49 |

Findings that should shape what we test next:

1. **"High" is hindsight, not a strategy.** `WorkerFunction.BuildEntryResult` sets `High` to the
   candle with the maximum close inside the exit window (`candlesWithinMarketHours.MaxBy(c => c.Close)`).
   No exit rule can reproduce it. The realistic number for backtest `d8e36f18` is the Hold column:
   PF 1.39 overall and flat in 2026. Treat "High" as an upper bound on what a perfect conditional
   exit could add (~+$9k here), which is itself an argument for plan 11 #1 (conditional exits).
2. **The edge is the immediate bounce.** Trades whose worst drawdown (MAE) stayed better than −1% won
   82% (Hold) / 91% (High) with PF 45+. Trades that dipped more than −1% won 19–22% and lost money in
   aggregate (PF 0.37 / 0.48). In other words, if the bounce has not started within a minute or two,
   the trade is usually a loser. This argues for (a) entering on the RSI *turn* rather than the RSI
   *level*, and (b) testing tighter stops and a "give up after N minutes flat" exit.
3. **Live stops fill far worse than the backtest models.** Backtest stop fills: median −2.0%, p10 −2.0%,
   worst −5.0%. Live stop fills: median **−2.9%**, p25 −4.25%, p10 −6.1%, worst −20.9%; 46% of live
   stops filled worse than −3%. Average live stop = −$374 on a $10k position (−3.7%). This alone
   explains most of the PF gap between backtest (1.39) and live (1.18). Sub-$2 low-float names are
   where this is worst. See Part D #1.
4. **Winners are fat-tailed.** Live: the top 10% of trades account for ~3.5× the net profit; the
   remaining 90% net negative. Median timed exit is only +1.0%. Anything that trims the tail
   (a lower target, a shorter hold) will hurt more than it looks; anything that lets the tail run
   (trailing stop, conditional exit) is where the upside is.
5. **Price bucket matters live.** Live by entry price: `<$2` PF 1.34 (312 trades, most of the profit),
   `$2–5` PF 1.22, `$5–10` PF **0.77** (−$4.3k), `$10+` PF 1.16. In the backtest (min price $2.50,
   float < 50M) `$20+` Hold is flat (PF 1.17). Worth a price-band split test.
6. **Time of day is not stable.** Backtest: 09:xx entries are the weakest hour (Hold PF 1.23) and
   13:xx the strongest (2.18). Live: 13:xx is the *worst* hour (PF 0.92) and 11:xx the best (1.30).
   Different filters and only 31 live days, so do not build a time gate off either alone; test
   `time >= 9:45` (skip the first 15 minutes) and `time < 15:00` as the only two candidates.
7. **Ticker concentration.** Backtest: CRWV alone is 64 of 526 trades. Live: WETO 37, BTCT 35, NG 34.
   Cooldown is 15 minutes, so one runner can be re-entered 20+ times a day. See Part D #4.
8. **ConditionalExit: true on the live strategy does nothing.** `SellWorker.cs:144` is still a TODO.
9. **Signal count differs hugely.** Backtest averaged ~0.8 trades/day; live averages 26/day. Live
   filters are much looser (float < 10M, close > 1, volume > 10k, `ema` RSI which is faster than
   `wilders`). The live filter set has never been backtested as-is; that is test #1 below.

## B. Backtestable today

Conventions: one filter per line (lines are ANDed). Unless stated, exits = stop 2% / target 10% /
timed 5m / cooldown 15m / fixed $5k, 2024-01-01 → today, so results are comparable to `d8e36f18`.
Read the **Hold** stats, not High. Run each as a family: baseline + one change at a time.

> **Caveat — `[5m]` / `[15m]` suffixes in backtests.** The backtest DataCache loads S3 aggregates
> verbatim and only 1m/1h/1d are stored (memory note 2026-08-25; no fix has landed since). Until
> Part D #2 is done, keep backtest filters on `[1m]`, `[1h]`, `[1d]`. Every `[5m]` line below is
> marked ⚠ and can be approximated with `[1m, 5]` (all of last 5 one-minute candles) meanwhile.

### B1. Calibrate the live strategy (do these first)

1. **Live filter set, verbatim, 2024→today.** Filters exactly as the paper bot runs them, exits
   2% / 15% / 5m, $10k fixed. This is the missing baseline; everything else is measured against it.
2. **RSI smoothing A/B.** Same as #1 with `rsi(14,30,70,wilders) < 30 [1m]`. `ema` (alpha 2/15) is
   ~2× faster than Wilder's (alpha 1/14), so it fires on shallower dips; we do not know which is
   better.
3. **Stop sweep.** #1 with stop 1%, 1.5%, 3%, 4%. Finding A2 says trades that dip >1% mostly lose,
   but finding A4 says winners are fat-tailed; the sweep tells us which dominates.
4. **Hold sweep.** #1 with timed exit 3m, 10m, 15m, 30m, 60m.
5. **Target sweep.** #1 with target 5%, 8%, 15%, 25%. Only 7 of 813 live trades hit 15%.
6. **Price-band split.** #1 with `close > 1 AND close < 2 [1m]`, `close >= 2 AND close < 5 [1m]`,
   `close >= 5 AND close < 10 [1m]`, `close >= 10 [1m]` as four runs. Live says `$5–10` loses.
7. **Trim the open.** #1 plus `time >= 9:45`. And separately `time < 15:00`.
8. **Daily liquidity floor.** #1 plus `volume > 500000 [1d]` (the `d8e36f18` run used 1M). Cheap
   way to reduce the stop-slippage names.

### B2. RSI-low variants (extend the proven edge)

9. **Enter on the turn, not the level.** Replace the RSI level with the cross back up:
   ```
   crosses_over(rsi(14,30,70,wilders), 30) [1m]
   ```
   Directly targets finding A2: the bounce has started before we buy. Expect fewer trades, higher
   win rate; the question is whether the missed first minute costs more than the avoided stops.
10. **Oversold within the last N bars AND recovering now** (two lines, both must hold):
    ```
    rsi(14,30,70,wilders) < 25 [1m, 5, any]
    rsi(14,30,70,wilders) > 30 [1m]
    ```
    Deeper washout (25) followed by a reclaim of 30 — a slower, more selective version of #9.
11. **Persistence.** `rsi(14,30,70,wilders) < 30 [1m, 3]` — oversold on three consecutive minutes,
    i.e. a sustained flush rather than a one-print wick.
12. **Two-timeframe washout.** `rsi(14,30,70,wilders) < 30 [1m]` plus
    `rsi(14,30,70,wilders) < 40 [1h]` — the hourly is also weak, so the dip is part of a larger
    sell-off. (⚠ `[5m] < 35` once 5m is fixed.)
13. **Dip inside a strong day (VWAP gate).** #1 plus `close > vwap() [1m]`. Classic "buy pullbacks
    above VWAP". Run the inverse (`close < vwap()`) too; it tells us whether the edge is trend
    continuation or capitulation.
14. **Dip inside an intraday uptrend (slope gate).** #1 plus `slope(sma(20), 10) > 0 [1m]` or, once
    5m works, ⚠ `slope(ema(9), 5) > 0 [5m]`.
15. **Volume climax on the signal bar.** #1 plus `volume > adv(30) [1m]` — the RSI-low minute printed
    above-average volume (a capitulation print). A stricter form, `volume > adv(30) [1m, 2]`,
    requires two heavy bars.
16. **Relative-volume proxy without arithmetic.** `adv(5) > adv(30) [1m]` (last 5 minutes averaged
    more volume than the last 30) or `adv(10) > adv(60) [1m]`. This is the only rvol-style
    expression the DSL supports today; real `rvol` is plan 11 #2.
17. **Daily oversold + intraday flush.** Multi-day washout, then buy the intraday dip:
    ```
    rsi(14,30,70,wilders) < 35 [1d]
    close < sma(20) [1d]
    rsi(14,30,70,wilders) < 30 [1m]
    ```
18. **Red-candle streak.** `close < open [1m, 4]` plus the RSI line: four straight red minutes into
    the signal. Cheap "capitulation shape" filter.
19. **Sitting on support.** `sr().near_support = 1 AND sr().support_strength > 0.6 [1h]` plus the
    RSI line. `support_resistance` is expensive (`Cost = 6`), so keep it on `[1h]`/`[1d]`.
20. **Large-cap / liquid version.** `float > 300000000`, `close > 20 [1m]`, `volume > adv(30) [1m]`,
    `rsi(14,30,70,wilders) < 25 [1m]`, stop 1%, target 3%, timed 15m. Same idea, different regime:
    smaller moves but stops fill where they are set. This is the honest comparison for finding A3.

### B3. RSI-high: momentum continuation (buy strength, not fade it)

You have tested RSI > 70; the version most likely to work long-only on low floats is *continuation*
with a volume and VWAP gate, not a fade (fading needs shorts — plan 11 #9).

21. **Breakout into overbought.** 
    ```
    crosses_over(rsi(14,30,70,wilders), 70) [1m]
    close > vwap() [1m]
    volume > adv(30) [1m, 2]
    float < 20000000
    ```
    Exits: stop 3%, target 15%, timed 15m. (Plan 11 #6 trailing stop is the natural exit here.)
22. **Sustained overbought.** `rsi(14,30,70,wilders) > 70 [1m, 5]` plus VWAP gate — momentum has
    held for 5 minutes. Higher bar than #21.
23. **MACD histogram turn.** `crosses_over(macd(12,26,9,ema).histogram, 0) [1m]` plus
    `close > vwap() [1m]` and `volume > adv(30) [1m]`. (⚠ better on `[5m]`.)

### B4. VWAP strategies

24. **VWAP reclaim.** `crosses_over(close, vwap()) [1m]`, `volume > adv(30) [1m]`,
    `float < 50000000`, `time >= 9:45`. Exits: stop 2%, target 6%, timed 30m.
25. **VWAP touch-and-hold.** `low <= vwap() [1m]` and `close > vwap() [1m]` and `close > open [1m]` —
    price tagged VWAP and closed back above it. Add `slope(vwap(), 10) > 0 [1m]` for a rising VWAP.
26. **VWAP loss (bearish, for when shorts exist).** Park until plan 11 #9.

### B5. Trend-following / pullback (longer holds)

27. **9-EMA pullback in an uptrend.** `close > sma(200) [1d]`, `sma(20) > sma(50) [1h]`,
    `crosses_over(close, ema(9)) [1h]`. Exits: stop 3%, target 8%, timed 1 day. (⚠ `[5m]` version
    for a faster variant.)
28. **Connors RSI(2).** `rsi(2,90,10,wilders) < 10 [1d]` and `close > sma(200) [1d]`,
    `volume > 1000000 [1d]`, `close > 10 [1d]`. Exit: timed 3 days, stop 5%, no target (or exit
    when `rsi(2) > 60`, which needs plan 11 #1). **Verify first** that the backtester handles a
    multi-day timed exit — `DateUtilities.GetEndDate` supports `Timespan.day`, but the worker's
    per-day candle window and the EOD-flatten clamp (2026-08-20) may cut it to the same-day close.
29. **Golden-cross pullback.** `crosses_over(sma(50), sma(200)) [1d, 20, any]` (cross happened in
    the last 20 sessions) and `rsi(14,30,70,wilders) < 40 [1d]`. Daily-timeframe swing idea; same
    multi-day-exit verification as #28.

### B6. Support/resistance breakout

30. **Break above weak resistance on volume.** 
    ```
    close > sr().resistance_upper [1h]
    sr().resistance_strength < 0.4 [1h]
    volume > adv(30) [1m, 2]
    ```
    Exits: stop 2%, target 8%, timed 30m. Plan 11 #5 (`highest(n)`) would make this an honest
    opening-range / Donchian breakout; today `sr()` is the only rolling-extreme primitive.

## C. Needs a capability we do not have

Items marked **(plan 11 #n)** are already in `plans/11-strategy-dsl-gaps.md`; the rest are new and
should be appended there when picked up.

| # | Strategy | Blocked on |
|---|---|---|
| C1 | RSI-low with **exit when RSI crosses back over 50** (or close crosses VWAP). Finding A1 puts the ceiling at roughly +$9k on `d8e36f18`. | Conditional exits **(plan 11 #1)** — the single highest-value item |
| C2 | RSI-low with a **trailing stop** (e.g. trail 1.5% from the high water mark once +2%) to keep the fat tail (A4) | Trailing stop **(plan 11 #6)** |
| C3 | "Give up" exit: **flat after N minutes if not +X%** (A2 says non-bouncers lose) | New: break-even / time-and-price exit rule; could be a special case of C1 |
| C4 | **True relative volume** `rvol > 3` (volume vs same-time-of-day baseline) | `rvol` **(plan 11 #2)** |
| C5 | `volume > 3 * adv(30)`, `close < 0.97 * sma(20)`, `close * volume > 1000000` (dollar volume) | DSL arithmetic **(plan 11 #3)** |
| C6 | **Gap-down capitulation revert**: `gap < -15% AND rsi(14) < 30 [1m]`; **gap-up fade** (needs shorts) | Gap / prev_close primitives **(plan 11 #4)** |
| C7 | **Opening-range breakout**: `close > highest(high, 30) [1m] AND time >= 10:00` | `highest/lowest(n)` **(plan 11 #5)** |
| C8 | **N-day-low washout**: `close < lowest(low, 20) [1d]` + intraday RSI dip | `highest/lowest(n)` **(plan 11 #5)** |
| C9 | **Bollinger %B mean reversion**: `close < bollinger(20,2).lower [1m]` and the reclaim `crosses_over(close, bollinger(20,2).lower)` | New: `bollinger(period, stdev)` function with `.upper/.middle/.lower/.percent_b/.bandwidth` — the standard MR indicator we lack |
| C10 | **Bollinger squeeze breakout**: `bollinger(20,2).bandwidth < X` then `close > bollinger(20,2).upper` | C9 + Keltner (`keltner()`) for the classic squeeze definition |
| C11 | **Stochastic / Williams %R** variants of every RSI idea | New: `stoch(k, d, smooth)` and `williams_r(period)` — cheap once the [FilterFunction] process exists |
| C12 | **ATR-scaled stops and targets** (stop = 1.5×ATR) so one strategy fits $1 and $50 names; ATR-based volatility filter | `atr()` **(plan 11 #8)** |
| C13 | **Short side**: fade RSI > 70 blowoffs, gap-up fades, VWAP-loss shorts | Shorts **(plan 11 #9)** |
| C14 | **Regime gating**: only take RSI-low when `spy.close > spy.sma(200) [1d]`; skip when VIX > 30 | Cross-symbol reference **(plan 11 #10)** |
| C15 | **Premarket-high break**: `close > premarket_high AND time < 10:00` | Premarket keywords **(plan 11 #16)** |
| C16 | **Day-change capitulation**: `day_change_pct < -20 AND rsi(14) < 30 [1m]` | New: `day_change_pct` (close vs today's open and vs prev close) — partly plan 11 #3/#4 |
| C17 | **Market-cap bands** (micro vs small) as an alternative to float | `market_cap` **(plan 11 #11)** |
| C18 | **Sector-relative strength** | Sector data **(plan 11 #14)** |
| C19 | **Squeeze setups** (high short interest + low float + volume) | Short interest data **(plan 11 #15)** |
| C20 | **Real-participation filter**: `transactions > 500 [1m]` (many prints vs one block) | New: expose `Bar.TransactionCount` as a `transactions` keyword (data already stored, plan 11 #16 mentions it) |
| C21 | **Bars-since / age**: "RSI crossed under 30 within the last 3 bars" | Mostly expressible via `[1m, 3, any]` + a second line (see B2 #10); a `bars_since(cond)` function would be cleaner |
| C22 | **Partial exits / scale-out** (sell half at +5%, trail the rest) | New: multi-leg exit settings in backtest + live |

## D. Engine and tooling work that changes what the results mean

1. **Model stop slippage in the backtest.** Live stop fills median −2.9% vs backtest −2.0% (finding
   A3). Options: fill stops at the next bar's open instead of the trigger price; or a configurable
   slippage in bps applied to exits (and entries) by price band. Until then, mentally haircut every
   backtest PF on sub-$5 names. Also compare live entry fills vs signal-bar close.
2. **Fix `[5m]`/`[15m]` in backtests.** Rebuild intraday candles from the 1m series in `DataCache`
   (or store 5m/15m in S3) and make `/filters/validate` reject unsupported backtest timeframes
   until then. Half the ideas above want a 5m gate.
3. **Parameter sweeps.** Most of Part B is "same filters, one knob changed". A batch/grid backtest
   (one request, N variants, one comparison table) would turn a week of clicking into an afternoon.
   Also a walk-forward split (fit on 2024–25, verify on 2026) — `d8e36f18` is flat in 2026, which
   we would not have noticed without the by-year split.
4. **Per-ticker daily entry cap.** A `MaxEntriesPerTickerPerDay` position setting (finding A7):
   CRWV = 12% of one backtest, WETO/BTCT/NG = 13% of live trades.
5. **Surface Hold as the headline.** The UI labels the hindsight column "High" / "sold at in-trade
   high" without saying it is unattainable. Rename to "Best-case (hindsight)" or move it below Hold.
6. **Honour or remove `ConditionalExit`/`AvoidOvernight`** on the strategy form (plan 11 #1, #13);
   the live strategy has both set and neither does anything.
7. **Live vs backtest reconciliation report.** Run the live filter set as a backtest over the same
   31 days and diff trade-by-trade. Any gap beyond slippage is a parity bug (plan 10 territory).

## Suggested order

1. B1 #1–#3 (calibrate the live set; stop sweep) and D1 (slippage) — these decide whether the paper
   bot's +18% is real.
2. B2 #9/#10 (enter on the turn) and B2 #13 (VWAP gate) — cheapest structural improvements.
3. D2 (5m in backtests), then re-run the ⚠ items.
4. Plan 11 #1 (conditional exits) + #6 (trailing stop) — unlock C1–C3, the biggest upside per A1/A4.
5. C9 (Bollinger) via the `add-filter-function` skill — the one missing indicator for this family.
