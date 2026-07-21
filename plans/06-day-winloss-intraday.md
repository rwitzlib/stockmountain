# Plan 6: `dayWins`/`dayLosses` + intraday balance extremes per equity point

## Goal

Each equity point in the backtest payload should carry (a) the number of winning and losing
trades **closed** that day, and (b) the intraday high and low of total balance. Daily P&L bars
gain a win-rate dimension (and a future calendar heatmap becomes possible), and drawdown can be
computed from intraday troughs instead of close-to-close only.

## How the backtest pipeline works (context for a fresh reader)

1. `POST /api/backtest` → `BacktestHandler.Create`
   (`packages/marketviewer-application/.../Market/Backtest/BacktestHandler.cs`) → invokes lambda.
2. `apps/backtester/Backtest.Lambda/OrchestratorFunction.cs` fans out per-day workers, then runs
   `packages/marketviewer-core/MarketViewer.Core/Services/BacktestPortfolioSimulator.cs` and
   persists the `BacktestResultResponse` to S3 (`backtestResults/{userId}/{id}/portfolio.json`).
3. `GET /api/backtest/result/{id}` returns that JSON (camelCase) to
   `apps/web/src/pages/BacktestDetailPage.tsx`.

**Where days are simulated** — `BacktestPortfolioSimulator.SimulateStrategy(...)`:

- Walks each trading day minute-by-minute (9:30–16:00 ET). Per minute:
  `SellPositions(...)` closes positions whose outcome's `SoldAt` matches the minute (appending a
  `BacktestExecutedTrade` and adding `outcome.Profit` to `dayProfit`), then `BuyPositions(...)`
  opens new ones.
- Per day it appends a `BacktestEquityPoint`
  (`packages/marketviewer-contracts/.../Models/Backtest/BacktestEquityPoint.cs` — currently
  `Date`, `StartCash`, `EndCash`, `TotalBalance`, `OpenPositions`, `MaxConcurrentPositions`,
  `DayProfit`, `TradesTaken`), where `TotalBalance = openPositions.Sum(q => q.StartPosition) + availableFunds`.

**Honest limitation to preserve in naming/docs:** the simulator has no candle data, so open
positions are marked **at cost** (`StartPosition`). The intraday balance series therefore moves
only when fills happen (a sale realizes profit into cash; a buy is balance-neutral). The new
extremes are "fill-to-fill realized balance extremes", not tick-level mark-to-market. That is
still strictly more informative than the end-of-day snapshot (e.g. a day that dipped hard at
10:00 and recovered by close currently looks flat). Document this in the XML docs and keep names
neutral (`IntradayHighBalance` / `IntradayLowBalance`).

> Coordinate with Plan 4 (skipped signals): both extend `BacktestEquityPoint` and
> `SimulateStrategy`. Implement sequentially, either order.

## Contract changes

`BacktestEquityPoint`:

```csharp
/// <summary>Trades closed this day with positive profit.</summary>
public int DayWins { get; set; }

/// <summary>Trades closed this day with negative profit.</summary>
public int DayLosses { get; set; }

/// <summary>Highest total balance observed during the day (positions marked at cost — fills-only granularity).</summary>
public float IntradayHighBalance { get; set; }

/// <summary>Lowest total balance observed during the day (positions marked at cost — fills-only granularity).</summary>
public float IntradayLowBalance { get; set; }
```

Zero defaults are fine for old payloads (the web treats `IntradayHighBalance == 0` as absent —
a real balance of exactly 0 is not a meaningful case here).

## Backend steps

All in `BacktestPortfolioSimulator`:

1. `SellPositions(...)`: add `ref int dayWins, ref int dayLosses` (it already takes refs);
   increment on `outcome.Profit > 0` / `< 0` where the trade is appended. Break-even trades
   (`Profit == 0`) count in neither — matching how `WinRatio` already excludes them
   (see the wins/losses LINQ in `SimulateStrategy`).
2. `SimulateStrategy(...)` day loop:
   - Before the minute loop: `var intradayHigh = float.MinValue; var intradayLow = float.MaxValue;`
     plus `dayWins = dayLosses = 0`.
   - Inside the minute loop, **after** the sell+buy calls for that minute, compute
     `var balance = openPositions.Sum(q => q.StartPosition) + availableFunds;` and fold into
     high/low. (This sum-per-minute mirrors the existing per-day `TotalBalance` computation;
     ~390 iterations/day over a small list — cheap. If profiling says otherwise, maintain the
     open-position sum incrementally instead.)
   - Also fold in the day's starting balance (`startCash` + open positions carried overnight)
     before the first minute so a day with no fills gets `high == low == TotalBalance`.
   - Write all four fields onto the day's `BacktestEquityPoint`.
3. Invariants to maintain: `IntradayLowBalance <= TotalBalance <= IntradayHighBalance`;
   `DayWins + DayLosses <= TradesTaken`-closed-that-day (note: trades *closed* today may have
   been *opened* on a prior day — `TradesTaken` counts opens, wins/losses count closes; do not
   assert equality).

## Web app steps (`apps/web/src`)

1. `types/types.ts` — `EquityPoint`: add
   `dayWins?: number; dayLosses?: number; intradayHighBalance?: number; intradayLowBalance?: number;`
2. `pages/BacktestDetailPage.tsx` `normalizeEquity()`: carry the four through.
3. `components/backtest/charts/DailyPnlChart.tsx` tooltip: add a "W / L" row
   (`{dayWins} W · {dayLosses} L`) when either is present and nonzero-or-old-data check passes
   (`dayWins != null && (dayWins > 0 || dayLosses > 0 || tradesTaken === 0)` is overthinking it —
   simplest honest gate: show the row only when `dayWins != null && intradayHighBalance` is
   present too, i.e. new payloads; otherwise omit).
4. `utils/backtestAnalytics.ts` `computeDrawdown(...)`: when intraday lows are present on all
   points, compute drawdown against the running high-water mark using
   `intradayHighBalance` for peaks and `intradayLowBalance` for troughs; fall back to
   `totalBalance` otherwise. The drawdown strip in
   `components/backtest/charts/EquityCurveCard.tsx` needs no change (it consumes the array).
   Label change: if intraday-based, the strip's min label already shows the number — optionally
   append "(intraday)" to the "Drawdown" strip label via a new prop; keep minimal.
5. A calendar heatmap is **out of scope** for this plan.

## API collection docs

Update the equity-point example in `api-collections/Api/Backtest/Get Backtest Results.yml`
(+ `(latest)`) with the four new fields.

## Tests

Simulator tests (`tests/marketviewer-core-unit-tests/`; create `BacktestPortfolioSimulatorTests.cs`
if none exists — the class is `public static`, drive it with hand-built `WorkerResponse` fixtures):

- One win + one loss closing the same day ⇒ `DayWins == 1`, `DayLosses == 1`; break-even trade
  counts in neither.
- A position opened day 1, closed day 2 ⇒ win/loss counted on day 2.
- Sell-then-recover within a day: balance dips below start (loss realized at 10:00, profit at
  15:00) ⇒ `IntradayLowBalance < TotalBalance` at day end; invariant
  low ≤ TotalBalance ≤ high on every point.
- No-fill day ⇒ high == low == TotalBalance.

Web: `npx tsc --noEmit` + `npm run build:dev`; old-payload fixture renders unchanged
(close-to-close drawdown, no W/L tooltip row).

## Backward compatibility

Old S3 results deserialize new ints/floats as 0; web gates on `intradayHighBalance` being present
and truthy before using intraday drawdown or showing W/L rows. No migration.

## Acceptance criteria

- [ ] `dotnet build stockmountain.sln` clean; simulator tests pass.
- [ ] Fresh backtest: every equity point satisfies low ≤ totalBalance ≤ high; W/L counts match
      trades closed per day (spot-check against `trades[]` grouped by `soldAt` date).
- [ ] Daily P&L tooltip shows W / L on new results; drawdown uses intraday troughs on new
      results and close-to-close on old ones.
