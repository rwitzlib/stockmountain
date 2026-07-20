# Plan 2: Per-trade MFE/MAE (`maxRunup` / `maxDrawdown`)

## Goal

Each executed trade in the backtest payload should carry its **maximum favorable excursion**
(`maxRunup`: the best unrealized profit reached while the position was open) and **maximum
adverse excursion** (`maxDrawdown`: the worst unrealized loss), in dollars, consistent with the
existing `profit` field. This answers "is my stop too tight? is my target too low?" per trade
instead of only via the aggregate max-potential ceiling, and unlocks a future MAE scatter and a
per-trade exit-efficiency metric (`profit / maxRunup`).

## How the backtest pipeline works (context for a fresh reader)

1. `POST /api/backtest` → `BacktestHandler.Create`
   (`packages/marketviewer-application/.../Market/Backtest/BacktestHandler.cs`) → invokes lambda.
2. `apps/backtester/Backtest.Lambda/OrchestratorFunction.cs` fans out one **WorkerFunction** per
   trading day, feeds worker responses into
   `packages/marketviewer-core/MarketViewer.Core/Services/BacktestPortfolioSimulator.cs`, and
   persists the result to S3 (`backtestResults/{userId}/{id}/portfolio.json`) via
   `BacktestRepository.PutCompleted`.
3. `GET /api/backtest/result/{id}` returns that JSON (camelCase) to
   `apps/web/src/pages/BacktestDetailPage.tsx`.

**Where per-trade candle data lives** — only in the worker.
`apps/backtester/Backtest.Lambda/WorkerFunction.cs` `GetBacktestResult(...)`:

- Fetches 1-minute bars from Polygon for the entry window and builds
  `candlesWithinMarketHours` (a `List<Bar>`; `Bar` from `Polygon.Client.Models` has
  `Open/High/Low/Close/Vwap/Timestamp`).
- Entry: `entryPrice = candlesWithinMarketHours.First().Vwap`, `shares`, `entryPosition`.
- Builds outcomes on `BacktestEntryResultCollection`: `Hold` (last candle in window — the real
  strategy exit), `High` (max-VWAP candle), then `CheckTakeProfit` / `CheckStopLoss` may overwrite
  either with an earlier fill.

The simulator (`BacktestPortfolioSimulator.SellPositions`) copies outcome fields onto
`BacktestExecutedTrade`, which is what `trades[]` in the payload contains. **The simulator has no
candle data**, so MFE/MAE must be computed in the worker.

> Coordinate with Plan 1 (`exitReason`): both plans touch `BacktestEntryResult`,
> `BacktestExecutedTrade`, and the same worker code block. Implement after Plan 1 and rebase.

## Contract changes

In `packages/marketviewer-contracts/MarketViewer.Contracts/Models/Backtest/`:

`BacktestEntryResult` and `BacktestExecutedTrade` both get:

```csharp
/// <summary>Best unrealized P&L (in dollars) reached between entry and this outcome's exit. Null when unavailable.</summary>
public float? MaxRunup { get; set; }

/// <summary>Worst unrealized P&L (in dollars, ≤ 0) reached between entry and this outcome's exit. Null when unavailable.</summary>
public float? MaxDrawdown { get; set; }
```

Nullable so old persisted results (which lack the fields) deserialize as null, and so the web can
distinguish "not computed" from 0. Note: `BacktestEntryStats` already has an *aggregate*
`MaxDrawdown` (equity-curve drawdown) — same name, different scope; keep both, the trade-level XML
docs should point out the distinction.

## Backend steps

1. **WorkerFunction.GetBacktestResult** (`apps/backtester/Backtest.Lambda/WorkerFunction.cs`):
   - Add a private static helper:

     ```csharp
     static (float runup, float drawdown) ComputeExcursions(
         List<Bar> candles, long exitTimestampMs, int shares, float entryPosition)
     ```

     Iterate candles with `candle.Timestamp <= exitTimestampMs`:
     `runup = max(runup, candle.High * shares - entryPosition)` (floor 0),
     `drawdown = min(drawdown, candle.Low * shares - entryPosition)` (cap 0).
     Uses High/Low so intrabar extremes count even though entries/exits fill at VWAP.
   - After the `Hold`/`High` outcomes are final (i.e. **after** the `CheckTakeProfit` and
     `CheckStopLoss` blocks, since those change `SoldAt`), compute excursions per outcome from
     `candlesWithinMarketHours` up to that outcome's `SoldAt` and assign
     `result.Hold.MaxRunup/MaxDrawdown` and `result.High.MaxRunup/MaxDrawdown`.
     `SoldAt` is a `DateTimeOffset` (Eastern); compare via `SoldAt.ToUnixTimeMilliseconds()`.
   - Consistency guard: clamp `MaxRunup = max(MaxRunup, Profit)` and
     `MaxDrawdown = min(MaxDrawdown, Profit)` so a TP/SL fill price is never outside the range
     (TP/SL profits are computed from configured values, not raw candle prices, so tiny
     inversions are otherwise possible).
2. **BacktestPortfolioSimulator.SellPositions**
   (`packages/marketviewer-core/.../BacktestPortfolioSimulator.cs`): copy
   `MaxRunup = outcome.MaxRunup, MaxDrawdown = outcome.MaxDrawdown` into the
   `new BacktestExecutedTrade { ... }` initializer.

## Web app steps (`apps/web/src`)

1. `types/types.ts` — `ExecutedTrade`: add `maxRunup?: number; maxDrawdown?: number;`
2. `pages/BacktestDetailPage.tsx` `normalizeTrades()`: pass both through when numeric
   (`t.maxRunup != null ? Number(t.maxRunup) : undefined`).
3. `components/backtest/BacktestTradesTable.tsx`: add two right-aligned columns, **MFE** and
   **MAE**, rendered as signed currency (green/red via `var(--chart-gain)`/`var(--chart-loss)`,
   matching the existing P&L column, helpers in `utils/formatters.ts`). Render `—` when absent.
   The table already sets a `min-w` for horizontal scroll — bump it (~980px).
4. Optional small win: in the "Trade P&L distribution" card annotation
   (`BacktestDetailPage.tsx`), when MFE data exists, add average exit efficiency
   `mean(profit / maxRunup)` over trades with `maxRunup > 0` (compute in
   `utils/backtestAnalytics.ts`). A dedicated MAE scatter chart is out of scope here.

## API collection docs

Add `"maxRunup": 61.20, "maxDrawdown": -18.75` to a sample trade in
`api-collections/Api/Backtest/Get Backtest Results.yml` (+ the `(latest)` variant).

## Tests

- `tests/backtest-lambda-unit-tests/.../WorkerExitLogicUnitTests.cs` (or a new
  `WorkerExcursionUnitTests.cs`): feed a hand-built candle list and assert
  runup/drawdown for (a) plain timed exit, (b) TP exit — excursion window truncates at the TP
  candle, (c) SL exit, (d) clamp guard (`MaxRunup >= Profit`).
- Simulator test: fields survive into `BacktestExecutedTrade`.
- Web: `npx tsc --noEmit` + `npm run build:dev` in `apps/web`.

## Backward compatibility

Nullable/optional end-to-end; old S3 results render `—` in the new columns. No migration.

## Acceptance criteria

- [ ] `dotnet build stockmountain.sln` clean; tests pass.
- [ ] Fresh backtest: every trade has `maxRunup >= profit >= maxDrawdown`, `maxRunup >= 0 >= maxDrawdown`.
- [ ] Hold vs High outcomes for the same entry can differ (different exit times ⇒ different windows).
- [ ] Trades table shows MFE/MAE columns; old backtests show `—`.
