# Plan 5: Entry snapshot — filter values at entry time, per trade

## Goal

Each executed trade should carry the numeric value of each entry-filter expression at the moment
of entry, e.g. for filters `["price > 1 && price < 20", "float < 50000000", "relative volume > 3"]`
a trade gets `entrySnapshot: { "price > 1 && price < 20": 4.82, "float < 50000000": 12400000,
"relative volume > 3": 7.1 }`. This shows "what the scanner saw" per trade and enables slicing
win rate by feature bucket (e.g. win rate by float, by relative volume) later.

**This is the most invasive of the six payload plans** — it reaches into the filter engine.
Budget an investigation step before committing to the API shape.

## How the backtest pipeline works (context for a fresh reader)

1. `POST /api/backtest` → `BacktestHandler.Create`
   (`packages/marketviewer-application/.../Market/Backtest/BacktestHandler.cs`) → invokes lambda.
2. `apps/backtester/Backtest.Lambda/OrchestratorFunction.cs` fans out one **WorkerFunction**
   (`apps/backtester/Backtest.Lambda/WorkerFunction.cs`) per trading day:
   - `ScannerService.GetStrategyEntries(request)`
     (`apps/backtester/Backtest.Lambda/Services/ScannerService.cs`) evaluates each entry filter
     over every ticker minute-by-minute and returns `StrategyEntry { Ticker, Start }`
     (`apps/backtester/Backtest.Lambda/Models/StrategyEntry.cs`) for minutes where **all** filters
     intersect. Internals: `GetResultsFromFilter(filter, date)` compiles the expression via
     `IndicatorExpressionEngine` (`packages/marketviewer-filters/MarketViewer.Filters/`), gets a
     per-ticker `FilterSession` (`MarketViewer.Filters/Sessions/FilterSession.cs`), and calls
     `session.Evaluate(...)` (scalar-only filters, e.g. float) or
     `session.EvaluateIncremental(...)` per minute. Per-filter/day results are cached to S3.
   - The worker then prices each entry from Polygon 1-minute bars, producing
     `BacktestEntryResultCollection` (ticker, entry, Hold/High outcomes).
3. `BacktestPortfolioSimulator`
   (`packages/marketviewer-core/MarketViewer.Core/Services/BacktestPortfolioSimulator.cs`)
   replays entries under capital constraints and emits `BacktestExecutedTrade` items → persisted
   to S3 `portfolio.json` → served by `GET /api/backtest/result/{id}` → rendered by
   `apps/web/src/pages/BacktestDetailPage.tsx`.

## Step 0 — investigation (do this first, adjust the plan if needed)

Read `MarketViewer.Filters` (`Parsing/ExpressionParser.cs`, `Sessions/FilterSession.cs`,
`Expressions/DataAccessExpression.cs`, `Expressions/ExpressionPlanner.cs`) and answer:

1. When a session evaluates a comparison like `relative volume > 3`, is the left-hand numeric
   value materialized somewhere retrievable, or folded away?
2. Is `FilterSession` stateful per ticker such that "last evaluated LHS value" is well-defined
   right after `Evaluate`/`EvaluateIncremental` returns true?
3. For compound expressions (`price > 1 && price < 20`), what is a sensible single value?
   (Recommended: the value of the **first comparison's left operand** — here `price` — since
   compound filters in this grammar compare the same underlying series; verify that assumption.)

Deliverable of step 0: choose between
**(A)** extend `FilterSession` with `bool TryGetLastLeftValue(out float value)` (preferred if the
LHS is already materialized), or
**(B)** a separate `EvaluateWithValue(...)` overload that recomputes the LHS once for matched
entries only. Either way the capture must be **opt-in** so the hot scanning path (thousands of
tickers × 389 minutes) pays nothing.

## Design constraints (why capture happens late, not during scanning)

- `GetResultsFromFilter` results are cached to S3 per filter/day; putting snapshot values into
  `StrategyEntry` would bloat that cache and invalidate existing entries (`StrategyEntry`
  serializes with terse names `t`/`s` deliberately).
- The scanner evaluates *every* ticker; only the intersected entries (and ultimately only
  *executed* trades) need values.

**Recommended shape:** after `ScannerService.GetStrategyEntries` computes the final intersected
entry list, run a **snapshot pass**: for each final `StrategyEntry` (ticker, minute) and each
filter, re-evaluate that single (ticker, minute) with value capture enabled, producing
`Dictionary<string, float> Snapshot` keyed by the raw filter string. The data needed
(`DataCache.GetStocksResponse(ticker, timeframe)`) is already in memory in the worker. Bound: the
snapshot pass costs O(entries × filters), not O(tickers × minutes).

## Contract changes

- `StrategyEntry` (worker-internal): add `[JsonIgnore] public Dictionary<string, float> Snapshot`
  — **JsonIgnore** so the S3 scanner cache format is untouched (verify how the cache serializes;
  if it round-trips through JSON with ignore-default settings, confirm the attribute suffices).
- `packages/marketviewer-contracts/.../Models/Backtest/BacktestEntryResultCollection.cs`:
  `public Dictionary<string, float> EntrySnapshot { get; set; }` (null when absent). The worker
  copies `entry.Snapshot` onto the collection in `WorkerFunction.GetBacktestResult`.
- `BacktestExecutedTrade`: `public Dictionary<string, float> EntrySnapshot { get; set; }` —
  copied in `BacktestPortfolioSimulator.SellPositions` from the position
  (`BacktestEntryResultCollection`), like `Ticker`/`BoughtAt` are today.

Size check: snapshot lives on every candidate in `universe.json` and every executed trade in
`portfolio.json`. For the reference run (2,951 trades × 3–4 filters) that's well under a MB
uncompressed in `portfolio.json`; `universe.json` is larger but already the big file and
compressed (`apps/backtester/Backtest.Lambda/Utilities/CompressionUtilities.cs` — confirm where
it's applied). If `universe.json` growth is a concern, snapshot only the entries that produced
results (post-pricing), which is the same set.

## Backend steps

1. Implement the chosen `FilterSession` capture API in `packages/marketviewer-filters` (step 0
   decision), with unit tests in `tests/marketviewer-filters-unit-tests/`.
2. `ScannerService`: add the snapshot pass at the end of `GetStrategyEntries` (behind the final
   intersection). Failures for a single (ticker, filter) must not drop the entry — log and omit
   that key.
3. `WorkerFunction.GetBacktestResult`: copy `entry.Snapshot` → `result.EntrySnapshot`.
4. `BacktestPortfolioSimulator.SellPositions`: copy `position.EntrySnapshot` →
   `trade.EntrySnapshot`.

## Web app steps (`apps/web/src`)

1. `types/types.ts` — `ExecutedTrade`: `entrySnapshot?: Record<string, number>;`
2. `pages/BacktestDetailPage.tsx` `normalizeTrades()`: validate it's an object of numbers, else
   undefined.
3. `components/backtest/BacktestTradesTable.tsx`: make rows expandable when `entrySnapshot`
   exists — clicking a row toggles a detail row listing each filter expression (monospace, like
   the Entry-filters rail card styling) with its value formatted sensibly
   (large values with `toLocaleString()`, small with 2 decimals). Keyboard accessible
   (button semantics, `aria-expanded`).
4. Feature-bucket win-rate analytics (win rate by float bucket etc.) are **out of scope** — this
   plan lands the data and a per-trade drill-in only.

## API collection docs

Add `"entrySnapshot": { "float < 50000000": 12400000, "relative volume > 3": 7.1 }` to a sample
trade in `api-collections/Api/Backtest/Get Backtest Results.yml` (+ `(latest)`).

## Tests

- `tests/marketviewer-filters-unit-tests/`: capture API returns the LHS value for simple and
  compound comparisons; capture disabled ⇒ zero overhead path unchanged (existing tests still green).
- `tests/backtest-lambda-unit-tests/.../ScannerServiceUnitTests.cs`: snapshot pass attaches values
  for final entries only; a failing filter evaluation omits the key but keeps the entry.
- Simulator: `EntrySnapshot` survives to `BacktestExecutedTrade`.
- Web: `npx tsc --noEmit` + `npm run build:dev`; rows without snapshot are not expandable.

## Backward compatibility

Old results have no `entrySnapshot`; field is nullable/optional everywhere; UI simply renders
non-expandable rows. Scanner S3 cache format unchanged (JsonIgnore) — verify by diffing a cached
filter/day object before/after.

## Acceptance criteria

- [ ] `dotnet build stockmountain.sln` clean; filters/scanner/simulator tests pass.
- [ ] Fresh backtest: every trade has `entrySnapshot` with one numeric entry per configured filter
      (minus any logged evaluation failures).
- [ ] Scanner throughput is not measurably regressed (capture is opt-in and runs only on final
      entries — sanity-check worker duration on a 1-day run before/after).
- [ ] Trades table rows expand to show the snapshot; old backtests unaffected.
