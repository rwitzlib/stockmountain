# Plan 11 — Strategy/filter capabilities to unlock new backtests

Backlog of filter-DSL and engine capabilities that do not exist today but would unlock new
strategy families to backtest. Ranked by expected value given that the best strategy found so
far ("RSI Low Revert", share `SFY_XICFGo1K5LLTreHBpw`) is intraday long-only mean reversion:
high trade count, ~54% win rate, profit factor 2.24, tight risk.

Each item notes where the current limitation lives. This is an idea backlog, not a single
hand-off unit — pick items individually.

## Tier 1 — directly extends the proven mean-reversion edge

### 1. Conditional (scan-based) exits — finish the existing stub
Exit when a filter expression becomes true, e.g. `rsi(14, 70, 30, wilders).value > 50` or
"close crosses back over vwap". This is the natural exit for mean reversion — today the only
exits are fixed stop %, fixed target %, and timed exit, which cut reversion trades at an
arbitrary point rather than when the reversion completes.

- Contract field already exists: `StrategyExitSettings.ConditionalExit` (`List<string>?`)
- Backtest implementation is ~85 lines commented out and non-compiling:
  `apps/backtester/Backtest.Lambda/WorkerFunction.cs:641-724` (references undefined `argument.Filters`)
- Live: `apps/paper-bot-runner/Optimus/Services/SellWorker.cs:110` has the TODO
- The dead `Other` portfolio timeline in `BacktestPortfolioSimulator.cs:266` was built for this
- Frontend type exists (`apps/web/src/types/strategy.ts:53`), no UI

### 2. Relative volume (`rvol`) as a filter keyword
"Volume is 3× normal for this ticker at this time of day" is a far better spike detector than
absolute volume thresholds. The RVOL study already exists (`MarketViewer.Studies/Studies/RVOL.cs`)
but is chart-only — it is not registered in `ExpressionParser._functions`, and its
`AverageVolume` input isn't reachable from the DSL either.

### 3. Arithmetic operators in the DSL (`+ - * /`)
The parser (`MarketViewer.Filters/Parsing/ExpressionParser.cs`) supports only comparison and
logical operators. No arithmetic means none of these are expressible:
- `volume > 3 * adv(30)` (relative volume without a dedicated rvol function)
- `close < 0.97 * sma(20)` (percent distance below a moving average — MAMR-style entries)
- `close < prev_close * 0.95` (gap sizing, once prev_close exists)
Unlocks a whole class of *relative* filters; today everything is absolute.

### 4. Gap / previous-session primitives
No `gap`, `prev_close`, or `overnight_change_pct` keyword exists. Gap-down capitulation
reversion and gap-up fades are classic small-cap setups and currently inexpressible. The data
is already on hand: daily bars are cached alongside minute data (`DataCache.cs:50-74`), and the
vendor snapshot model has `PreviousDay`/`TodaysChangePerc` (`Massive.Client/Models/Snapshot.cs:16,22`)
used only for live pricing.

### 5. `highest(n)` / `lowest(n)` rolling extremes
"Close breaks above the 30-bar high" (opening-range breakout, Donchian breakout, n-day
low washout) is not expressible: range mode (`[1m, 30, all]`) compares each bar against
*itself* per candle, so `close > high [1m, 30, all]` can never be true. Need
`close > highest(high, 30)`-style functions. (Partial proxy today:
`support_resistance().resistance` uses Donchian internally.)

### 6. Trailing stop exit
Does not exist in backtest or live (`StrategyExitSettings` has no such member). Lets breakout
and momentum variants run winners while keeping the tight-risk profile.

## Tier 2 — new strategy families

### 7. Session-anchored VWAP in the DSL
Filter keyword `vwap` is the *per-bar* vendor VWAP (`DataAccessExpression.cs:86`), not the
session-cumulative VWAP traders mean. VWAP-reclaim and VWAP-fade strategies need a true
`session_vwap` (or anchored VWAP) keyword. Note the existing VWAP *study* resets on the UTC
day (`Studies/VWAP.cs:19,23`), i.e. 20:00 ET, so it isn't a correct reference implementation.

### 8. `atr()` function + ATR-based stops/targets
ATR is already computed privately inside `SupportResistanceFunction.ComputeAtrSeries` but not
registered as a DSL function. Exposing it enables volatility filters (`atr(14) > x`) and —
bigger win — volatility-scaled exits (stop = 1.5×ATR) so one strategy adapts across quiet and
wild tickers instead of a fixed percent.

### 9. Short-side support
Everything is long-only. The mirror of the proven edge — fading RSI > 70 blowoffs on
low-float spikes — is the classic small-cap short. Large lift (backtest engine, simulator,
Alpaca adapter all assume long), and borrow availability/fees on low-floats make the backtest
optimistic, but it's the most obvious unexplored half of the strategy space.

### 10. Market-regime / cross-symbol reference
Filters evaluate only the candidate ticker; you cannot express "only enter when SPY is above
its 200-day SMA" or "VIX below 30". A reference-symbol namespace (e.g. `spy.close`,
`spy.sma(200) [1d]`) would allow regime-gating every strategy — often the difference between a
strategy that survives 2022-style tape and one that doesn't.

### 11. `market_cap` keyword
Data is already loaded per ticker (`TickerDetails.MarketCap`); the Gen-1 `FilterType.MarketCap`
enum member is orphaned. Trivial add in `DataAccessExpression` — float is the template.
Also: `FreeFloatPercent` is fetched then dropped (`StockFloat.cs:18`, never copied to
`TickerDetails`) — worth carrying through at the same time.

### 12. Percentage position sizing — fix or remove the stub
`PositionType.Percentage` is offered in the UI (`PositionSettingsForm.tsx:12`) but produces
**0 shares live** (`AlpacaAdapter.cs:55` falls to `_ => 0`) and raw-dollar sizing in backtest
(`WorkerFunction.cs:411`). Beyond the trap, real percentage sizing gives compounding — a
fixed-$ backtest materially understates long-run growth of a winning strategy.

### 13. `AvoidOvernight` — implement or remove
In the contract, set by the UI, displayed on five pages, never read by any engine
(`StrategyExitSettings.cs:25`). Users (including us) will assume it works.

## Tier 3 — data acquisitions (external sourcing required)

### 14. Sector / industry
Not modeled anywhere. Enables sector filters and sector-relative strength.

### 15. Short interest / borrow data
Not modeled. Squeeze setups (high SI + low float + volume spike) are a distinct small-cap
family; also prerequisite for honest short-side backtests (#9).

### 16. Session/premarket access
Premarket bars are ingested (00:00–23:59 ET, `AggregatorFunction.cs:344`) and warm up
indicators, but scanning is hard-clipped to 09:30–16:00 (`ScannerService.cs:45-53`). No
`is_premarket` keyword, no `premarket_high`/`premarket_low`. Premarket high break at the open
is a common momentum entry. Also `Bar.TransactionCount` is stored but not filterable — a
decent proxy for "real" participation vs a few big prints.

## Known bugs that skew existing/future backtests (fix before trusting results)

- ~~**`adv()` averages the whole series, not the last `period` bars**~~ — **FIXED 2026-08-16**
  (`AdvFunction.cs` is now a rolling volume SMA with incremental support; guarded by
  `GoldenIndicatorTests` `adv(*)` cases in plan 14). Any strategy that used `adv()` before this
  date was comparing against a wrong number and should be re-run.
- **`macd(...).signal` / `.histogram` emit `0` (not "no value") for bars before the signal line
  has warmed up** (`MacdFunction.cs` sets `Signal = 0, Histogram = 0` and still adds the bar).
  `histogram < 0.5`-style filters can fire on warm-up bars. Found by plan-14 golden tests
  (tolerated via `WarmupPlaceholderKeys` in `GoldenIndicatorTests`; remove that set when fixed).
- ~~**`NOT` binds to the primary, not the comparison**~~ — **FIXED 2026-08-16** (`ExpressionParser.ParseComparison`;
  guarded by plan-14 `GoldenFilterOutcomeTests not-unary`).
- ~~**Comparing a data/indicator series with a dot-field series throws**~~ (`close > macd(...).signal`) —
  **FIXED 2026-08-16** (`RangeEvaluationHelper.NormalizeMixedSeries`; plan-14 outcome cases).
- ~~**Backtester `UpdateLatestCandle` mis-anchors candles after a missing minute and mutates cached minute
  bars shared with concurrent filters**~~ — **FIXED 2026-08-16** (plan-14 `GoldenCandleFormingTests`,
  `GoldenScannerTests`). Backtests of illiquid names or multi-timeframe strategies before this date are suspect.
- **No parenthesized logical grouping** — `ParseExpression` is a flat fold with no AND/OR
  precedence (`ExpressionParser.cs:299-324`); `a AND (b OR c)` silently mis-parses. Either add
  grouping or reject `(` after a logical operator.
- **VWAP study resets at UTC midnight** (20:00 ET) — wrong session boundary (`Studies/VWAP.cs:19,23`).
- **Frontend indicator params contradict backend**: MACD `source` offers `close` (invalid),
  RSI offers only `sma|ema` — `wilders` unreachable (`apps/web/src/config/indicators.ts:59,77`
  vs `MacdFunction.cs:13`, `RsiFunction.cs:16`).
