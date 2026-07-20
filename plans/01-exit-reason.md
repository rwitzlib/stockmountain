# Plan 1: `exitReason` on every backtest trade

## Goal

Every executed trade in the backtest result payload should say *why* it exited:
`takeProfit`, `stopLoss`, `timedExit`, `endOfData`, or `soldAtHigh`. Today the payload only has
`stoppedOut: bool`, which is `true` for **both** stop-loss and take-profit exits, so the web UI
(BacktestDetailPage trades table) has to guess the reason from the profit sign and hold duration.
This is the highest-value missing field: it makes trade chips exact and unlocks P&L-by-exit-type
analytics later.

## How the backtest pipeline works (context for a fresh reader)

1. `POST /api/backtest` → `packages/marketviewer-application/.../Market/Backtest/BacktestHandler.cs`
   `Create()` stores a DynamoDB record and invokes the backtest lambda.
2. `apps/backtester/Backtest.Lambda/OrchestratorFunction.cs` fans out one **WorkerFunction** per
   trading day, then feeds all worker responses into
   `packages/marketviewer-core/MarketViewer.Core/Services/BacktestPortfolioSimulator.cs`, and
   persists the final `BacktestResultResponse` to S3 via
   `BacktestRepository.PutCompleted` (`packages/marketviewer-infrastructure/.../BacktestRepository.cs`,
   key `backtestResults/{userId}/{id}/portfolio.json`).
3. `GET /api/backtest/result/{id}` (`BacktestHandler.GetResult`) returns that S3 JSON verbatim.
   JSON is camelCase (see any saved `portfolio.json`; e.g. fields `boughtAt`, `stoppedOut`).
4. The web app consumes it in `apps/web/src/pages/BacktestDetailPage.tsx` (`normalizeTrades`).

**Where exits are decided** — `apps/backtester/Backtest.Lambda/WorkerFunction.cs`,
`GetBacktestResult(...)` (~lines 146–320):

- Builds `BacktestEntryResultCollection` with two outcomes per candidate entry:
  - `Hold` = sell at the **last** candle inside the timed-exit window (`GetStrategyEnd`) —
    i.e. the real strategy exit. `StoppedOut = false` initially.
  - `High` = sell at the max-VWAP candle in the window (the "max potential" ceiling).
- `CheckTakeProfit(...)` then overwrites both outcomes with the TP fill and sets
  `StoppedOut = true`.
- `CheckStopLoss(...)` overwrites again when the stop fires first (same-bar tie goes to the
  stop — keep that behavior), also `StoppedOut = true`.

So the worker *knows* the reason at the moment it sets each outcome; it just doesn't record it.

The simulator (`BacktestPortfolioSimulator.SellPositions`) later copies outcome fields onto
`BacktestExecutedTrade` — the object that ends up in the payload's `trades[]`.

## Contract changes

New enum in `packages/marketviewer-contracts/MarketViewer.Contracts/Enums/Backtest/`
(next to `BacktestStatus`):

```csharp
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BacktestExitReason
{
    timedExit,   // rode the timed-exit window to its final candle
    takeProfit,  // profit target filled
    stopLoss,    // stop filled
    endOfData,   // candles ran out before the window closed (halt/delisting/no data)
    soldAtHigh   // the "high" (max potential) portfolio's natural exit
}
```

(camelCase member names keep the serialized JSON consistent with the rest of the payload;
if repo style prefers PascalCase members, use `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`.)

Add to **both** models in `packages/marketviewer-contracts/.../Models/Backtest/`:

- `BacktestEntryResult`: `public BacktestExitReason? ExitReason { get; set; }`
- `BacktestExecutedTrade`: `public BacktestExitReason? ExitReason { get; set; }`

Keep `StoppedOut` untouched (mark `/// deprecated in favor of ExitReason` in the XML doc) —
old persisted results and any other consumers still rely on it.

## Backend steps

1. **WorkerFunction.GetBacktestResult** (`apps/backtester/Backtest.Lambda/WorkerFunction.cs`):
   - Initial `Hold` outcome: `ExitReason = timedExit`, **except** when the last candle is earlier
     than the computed window end (`entryEnd`) minus one minute — then `endOfData`. Concretely:
     compare `hold.Timestamp` against `entryEnd`; if the window was cut short because
     `candlesWithinMarketHours` ran out, it's `endOfData`. (An exact-boundary candle is `timedExit`.)
   - Initial `High` outcome: `ExitReason = soldAtHigh`.
   - In the `CheckTakeProfit` block: set `ExitReason = takeProfit` on both `Hold` and `High`
     alongside the existing `StoppedOut = true` assignments.
   - In the `CheckStopLoss` block (inside the tie-break `if`): set `ExitReason = stopLoss` on both.
2. **BacktestPortfolioSimulator.SellPositions**
   (`packages/marketviewer-core/MarketViewer.Core/Services/BacktestPortfolioSimulator.cs` ~line 159):
   copy `ExitReason = outcome.ExitReason` into the `new BacktestExecutedTrade { ... }` initializer.
3. Nothing else in the pipeline needs changing — orchestrator and repository serialize whatever is
   on the models.

## Web app steps (`apps/web/src`)

1. `types/types.ts` — `ExecutedTrade`: add `exitReason?: 'timedExit' | 'takeProfit' | 'stopLoss' | 'endOfData' | 'soldAtHigh';`
2. `pages/BacktestDetailPage.tsx` `normalizeTrades()`: carry the field through
   (`exitReason: typeof t.exitReason === 'string' ? t.exitReason : undefined`).
3. `components/backtest/BacktestTradesTable.tsx` — `exitChip(trade)` currently infers:
   `stoppedOut && profit > 0 → Target`, `stoppedOut → Stop`, else `Timed`. Change to: use
   `trade.exitReason` when present (`takeProfit → Target`, `stopLoss → Stop`,
   `timedExit → Timed`, `endOfData → Ended` with muted styling), and keep the current inference
   as the fallback for old results. Remove the header note in that card saying the reason is
   inferred ("an explicit exitReason field would make these chips exact") — condition it on
   whether any visible trade lacks `exitReason`.
4. Optional (nice, small): in `utils/backtestAnalytics.ts` add an exit-reason breakdown
   (count + net P&L per reason) and surface it as a fourth line under the "Time in trade" panel
   annotation. Don't build a new chart in this plan.

## API collection docs

Update the response examples in `api-collections/Api/Backtest/Get Backtest Results.yml` and
`Get Backtest Results (latest).yml` to include `"exitReason": "takeProfit"` on a sample trade.

## Tests

- `tests/backtest-lambda-unit-tests/Backtest.Lambda.UnitTests/WorkerExitLogicUnitTests.cs`
  already exercises `CheckStopLoss`/`CheckTakeProfit`. Add worker-level cases asserting:
  - no TP/SL hit → `hold.ExitReason == timedExit`, `high.ExitReason == soldAtHigh`
  - TP hit → both outcomes `takeProfit`
  - SL hit first (and the same-bar tie) → both outcomes `stopLoss`
  - candle series ending before the window → `endOfData`
- Simulator: add/extend a test (see `tests/marketviewer-core-unit-tests/`) asserting
  `BacktestExecutedTrade.ExitReason` equals the outcome's reason after `Simulate(...)`.
- Web: `cd apps/web && npx tsc --noEmit -p tsconfig.json` and `npm run build:dev` must pass.

## Backward compatibility

- Old `portfolio.json` files in S3 have no `exitReason`; the C# property is nullable and the
  web field optional, with chip inference as fallback — no migration needed.
- `stoppedOut` keeps being written so nothing that reads it breaks.

## Acceptance criteria

- [ ] `dotnet build stockmountain.sln` clean; new + existing tests pass.
- [ ] A fresh backtest's `portfolio.json` shows `exitReason` on every trade in all portfolios.
- [ ] TP exits report `takeProfit` (not just `stoppedOut: true`); stops report `stopLoss`;
      same-bar TP/SL tie reports `stopLoss`.
- [ ] Trades table chips read from `exitReason`, old backtests still render via fallback.
