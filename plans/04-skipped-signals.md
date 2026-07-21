# Plan 4: Per-day skipped-signal counts in the equity series

## Goal

Each equity point in the backtest payload should report how many entry signals the portfolio
simulation **saw** that day and how many it **skipped** — split by reason: insufficient cash vs
max-concurrent-positions. The backtester already makes these decisions and throws the counts
away. Surfacing them quantifies opportunity cost ("your filters found 141 signals; you could only
take 10") and gives direct evidence for tuning `MaxConcurrentPositions`, position size, and
starting balance.

## How the backtest pipeline works (context for a fresh reader)

1. `POST /api/backtest` → `BacktestHandler.Create`
   (`packages/marketviewer-application/.../Market/Backtest/BacktestHandler.cs`) → invokes lambda.
2. `apps/backtester/Backtest.Lambda/OrchestratorFunction.cs` fans out per-day workers (each
   worker returns every candidate entry for its day as `BacktestEntryResultCollection` items in a
   `WorkerResponse.Results`), then runs
   `packages/marketviewer-core/MarketViewer.Core/Services/BacktestPortfolioSimulator.cs` and
   persists the result to S3 (`backtestResults/{userId}/{id}/portfolio.json`).
3. `GET /api/backtest/result/{id}` returns that JSON (camelCase) to
   `apps/web/src/pages/BacktestDetailPage.tsx`.

**Where signals get skipped** — `BacktestPortfolioSimulator`:

- `SimulateStrategy(type, ...)` walks each trading day minute-by-minute (9:30–16:00 ET), calling
  `SellPositions(...)` then `BuyPositions(...)` per minute, and appends one
  `BacktestEquityPoint` per day (fields today: `Date`, `StartCash`, `EndCash`, `TotalBalance`,
  `OpenPositions`, `MaxConcurrentPositions`, `DayProfit`, `TradesTaken`).
- `BuyPositions(...)` (~line 182) iterates `entry.Results.Where(r => r.BoughtAt == timestamp)`
  and, per candidate: skips silently when `GetOutcome(type, result) is null` (no outcome for this
  strategy variant — **not** an opportunity miss, don't count it), skips when
  `availableFunds < positionSettings.Model.Size` (**cash skip**), skips when
  `openPositions.Count >= positionSettings.MaxConcurrentPositions` (**concurrency skip**),
  otherwise takes the trade. Each candidate's `BoughtAt` matches exactly one minute, so a skipped
  signal is counted exactly once per day — no dedup needed.

Note the simulation runs once per portfolio variant (`hold`, `high`, `other`), so counts are
per-portfolio (they will usually differ, because each variant frees capital at different times).
That is correct and desirable — the counts live on each portfolio's own `equity[]`.

> Coordinate with Plan 6 (`dayWins`/`dayLosses` + intraday extremes): both extend
> `BacktestEquityPoint` and `SimulateStrategy`. Implement sequentially, either order.

## Contract changes

`packages/marketviewer-contracts/MarketViewer.Contracts/Models/Backtest/BacktestEquityPoint.cs`:

```csharp
/// <summary>Entry signals with a valid outcome that the simulation evaluated this day.</summary>
public int SignalsSeen { get; set; }

/// <summary>Signals not taken because cash was below the position size.</summary>
public int SkippedFunds { get; set; }

/// <summary>Signals not taken because the max-concurrent-positions cap was hit.</summary>
public int SkippedConcurrency { get; set; }
```

(`TradesTaken` already exists; invariant: `SignalsSeen == TradesTaken + SkippedFunds + SkippedConcurrency`.)
Plain ints defaulting to 0 are fine for old payloads: the web treats "all zeros while
`tradesTaken > 0`" as "data not present" (see web steps).

## Backend steps

1. `BuyPositions(...)`: add `ref int signalsSeen, ref int skippedFunds, ref int skippedConcurrency`
   (or refactor to return a small counters struct — pick whichever reads cleaner in context; the
   method already takes two refs). Increment `signalsSeen` after the `GetOutcome(...) is null`
   check passes; replace the two silent `continue`s with counted ones. **Order note:** check the
   concurrency cap before the cash check, or count a candidate failing both as
   `SkippedConcurrency` — pick one rule, document it in the XML doc, and test it. (Recommended:
   concurrency first — with the cap hit, cash is moot.)
2. `SimulateStrategy(...)`: zero the three counters per day alongside `tradesTakenToday`, pass
   them through the minute loop, and write them onto the day's `BacktestEquityPoint`.
3. Nothing else changes — orchestrator/repository serialize the model as-is.

## Web app steps (`apps/web/src`)

1. `types/types.ts` — `EquityPoint`: add `signalsSeen?: number; skippedFunds?: number; skippedConcurrency?: number;`
2. `pages/BacktestDetailPage.tsx` `normalizeEquity()`: carry the three through as numbers when present.
3. Surface it (keep it modest in this plan):
   - `components/backtest/charts/DailyPnlChart.tsx` tooltip: after the "Trades" row add
     "Signals" (`signalsSeen`) and, when nonzero, "Skipped" (`skippedFunds + skippedConcurrency`,
     with the split in parentheses).
   - `components/backtest/charts/EquityCurveCard.tsx` crosshair tooltip: same two rows.
   - Verdict KPI: in `pages/BacktestDetailPage.tsx` there is a 8-tile KPI grid (`KpiTile`
     components). Add a "Signal coverage" tile when the data exists: value =
     `taken / signalsSeen` as a percent over the whole run; sub = "X of Y signals taken".
     Compute totals in `utils/backtestAnalytics.ts` from the equity array. When every point has
     `signalsSeen === 0` (old payloads), omit the tile (grid handles 8 or 9 tiles; if 9 looks
     unbalanced, replace the "Universe" tile's sub-line instead — implementer's call, note it in
     the PR).

## API collection docs

Update the equity-point example in `api-collections/Api/Backtest/Get Backtest Results.yml`
(+ `(latest)`) with `"signalsSeen": 141, "skippedFunds": 96, "skippedConcurrency": 35`.

## Tests

Simulator unit tests (`tests/marketviewer-core-unit-tests/`; if no simulator test file exists yet,
create `BacktestPortfolioSimulatorTests.cs` — the class is `public static`, easy to drive with
hand-built `WorkerResponse` fixtures):

- 3 same-minute signals, `MaxConcurrentPositions = 1`, ample cash ⇒ day 1:
  `TradesTaken = 1`, `SkippedConcurrency = 2`, `SkippedFunds = 0`, `SignalsSeen = 3`.
- Cash below `Model.Size` after the first fill ⇒ `SkippedFunds` counted.
- A variant-less candidate (`GetOutcome` null, e.g. `Other` sim over hold/high-only results) is
  **not** counted in `SignalsSeen`.
- Invariant holds on every equity point: seen = taken + skippedFunds + skippedConcurrency.

Web: `npx tsc --noEmit` + `npm run build:dev` in `apps/web`; old-payload fixture hides the new tile.

## Backward compatibility

Old S3 results deserialize the ints as 0. The web hides skip-derived UI when `signalsSeen` is 0
across all points, so old backtests look unchanged. No migration.

## Acceptance criteria

- [ ] `dotnet build stockmountain.sln` clean; simulator tests pass.
- [ ] Fresh backtest: equity points satisfy the seen/taken/skipped invariant; hold vs high
      counts may differ.
- [ ] Daily P&L and equity tooltips show Signals/Skipped; KPI tile shows run-level coverage.
- [ ] Old backtests render with no zeroed-out skip UI.
