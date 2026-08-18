# Plan 14 — Golden filter tests: real Massive data + independent reference values

> **Status 2026-08-16 — ALL THREE PHASES IMPLEMENTED (uncommitted).**
> Tooling: `tools/golden/{fetch_fixtures,compute_reference,compute_outcomes}.py` + README. Fixtures: 7
> (AAPL/NVDA 1m+1d, TSLA 1m across DST, SPY 1m half-day, SPY 1h; 5.4 MB incl. reference/outcomes).
> Filters project (`Golden/`): `GoldenIndicatorTests` (338 cases), `GoldenFilterOutcomeTests` (41 scripts,
> expected outcomes computed independently in Python; 2 S/R snapshot cases blessed; 1 `knownBug` held as an
> expected-failure), `GoldenManifestTests`, `GoldenReplay` — 493 tests. Backtest.Lambda project (`Golden/`):
> `GoldenCandleFormingTests` (33), `GoldenScannerTests` (7, real `DataCache.Setup` + `ScannerService` over a
> preloaded `IMarketCache`). Bugs found and fixed along the way are listed under §Findings; plan-11 updated.
> Acceptance probes done: re-introducing the old `adv()`, no-clone `UpdateLatestCandle`, and the pre-08-06
> `MergePreviousPeriod` guard each fail multiple golden tests.

## Context

Entry filters (`rsi(14) < 30 [5m,3]`, `close crosses over sma(200) [1d]`, …) are the core of
every scan, backtest and live strategy. They are evaluated by `IndicatorExpressionEngine` in
`packages/marketviewer-filters/MarketViewer.Filters` (~4.6k lines: parser, planner, 7 indicator
functions, comparison/logical operators, `FilterSession` incremental evaluation), and wired into
the backtester by `apps/backtester/Backtest.Lambda/Services/ScannerService.cs` +
`DataCache.cs` (multi-timeframe candle rebuild, `MergePreviousPeriod`, `UpdateLatestCandle`).

Test coverage today (`tests/marketviewer-filters-unit-tests`, 53 tests) is almost entirely
5-bar synthetic series (`Close = 100, 102, 104…`) with hand-picked thresholds. Exactly one test
loads real data (`TestData/tsla_1_minute_2025-09-27.json`) and it is commented out. Synthetic
tests have not caught the bugs that actually mattered:

- `adv()` averages the whole series instead of the last `period` bars (plan 11).
- `MergePreviousPeriod` one-bar-overlap guard broke `sma(200) [1d]` (fixed 2026-08-06, memory
  `backtest-reliability-2026-07`).
- VWAP study resets at UTC midnight instead of the ET session boundary (plan 11).
- Flat AND/OR parsing with no precedence/grouping (plan 11).
- The warm-Lambda stale-data leak and S3 cache key bugs — all in the wiring, not the math.

None of those surface with 5 bars, one timeframe and no session boundaries. This plan adds a
**golden test suite**: real Massive bar data committed as fixtures, indicator values computed
by an *independent* implementation, and recorded filter outcomes as a regression net.

## Decisions (locked 2026-08-16 — do not re-litigate)

- **Three layers, in priority order**: (1) golden indicator values vs an independent reference,
  (2) golden filter outcomes (snapshot/regression), (3) session-boundary + multi-timeframe
  fixtures through the backtester's `DataCache`/`ScannerService` path. Layer 1 is what earns
  the word "golden" — a self-generated expected value only enshrines current bugs.
- **Fixtures are Massive-format verbatim** (`StocksResponse` JSON as returned by
  `MassiveClient.GetAggregates`), so tests exercise the same deserialization the Lambda uses.
  No hand-built models. Provenance (ticker, range, timeframe, fetch date, adjusted flag) is
  embedded in each fixture file.
- **Reference values are computed in Python** (`pandas` + `ta`/`pandas-ta`; TA-Lib optional)
  by a script committed alongside the fixtures, run manually, output committed as JSON. The
  C# tests never call Python. Tolerances are explicit per indicator (see §1c).
- **Fixture budget is small and fixed**: ~4 tickers, 1-minute for ~5 trading days each plus 1-day
  for ~2 years, one 1-hour month. Target < 15 MB total in `TestData/Golden/`. Do not commit
  months of minute data; if a test needs more, it's an integration test, not a golden test.
- **Layer 2 outcomes are blessed only after known plan-11 bugs are fixed** or the affected
  cases are annotated `KnownBug` and asserted against the *correct* value with `Skip`. Never
  bless a wrong result silently.
- **Re-blessing is an explicit flag** (`GOLDEN_UPDATE=1`), never automatic. A diff in blessed
  output must be reviewed like a code change.
- Fixture fetch script lives in the repo and is one command; refreshing fixtures is a reviewed
  PR that regenerates both bars and reference values together.

## Layout

```
tests/marketviewer-filters-unit-tests/MarketViewer.Filters.UnitTests/
  Golden/
    GoldenFixture.cs                 # loads bars + reference JSON, exposes indexed access
    GoldenIndicatorTests.cs          # layer 1
    GoldenFilterOutcomeTests.cs      # layer 2
    GoldenSessionBoundaryTests.cs    # layer 3 (filters-package portion)
  TestData/Golden/
    manifest.json                    # list of fixtures + provenance + git-tracked hash
    bars/
      AAPL_1m_2025-06-02_2025-06-06.json
      AAPL_1d_2023-06-01_2025-06-06.json
      TSLA_1m_2025-03-07_2025-03-11.json     # DST change 2025-03-09
      SPY_1m_2024-11-27_2024-12-02.json      # half day 2024-11-29 (day after Thanksgiving)
      SPY_1h_2025-05-01_2025-05-30.json
      NVDA_1m_2025-06-02_2025-06-06.json     # heavy pre-market volume / gaps
      NVDA_1d_2023-06-01_2025-06-06.json
    reference/
      AAPL_1m_2025-06-02_2025-06-06.indicators.json
      ...                                     # one per bars file
    outcomes/
      filters.json                            # layer 2: script → list of true timestamps per fixture

tests/backtest-lambda-unit-tests/Backtest.Lambda.UnitTests/
  Golden/
    GoldenScannerTests.cs            # layer 3 (backtester wiring portion)

tools/golden/                        # new folder at repo root
  fetch_fixtures.py                  # Massive REST → bars/*.json (uses MASSIVE_API_KEY)
  compute_reference.py               # bars/*.json → reference/*.indicators.json
  requirements.txt
  README.md
```

Add `<None Update="TestData\Golden\**\*.json"><CopyToOutputDirectory>PreserveNewest` to the
filters test csproj (the existing TSLA fixture uses `Always`; `PreserveNewest` is fine and
faster). Backtest.Lambda tests reference the same folder via a `<Content Include=…Link=…>` so
there is one copy of the data.

## Fixture selection (why each one)

| Fixture | Stress target |
|---|---|
| AAPL 1m, 5 normal days | baseline; enough bars for `sma(200)`, `rsi(14)`, `macd(12,26,9)` warm-up |
| AAPL/NVDA 1d, 2 years | `sma(200) [1d]`, `adv(20) [1d]`, previous-period merge |
| TSLA 1m across 2025-03-09 DST | ET offset change mid-fixture; `evaluationTime` math in `ScannerService` |
| SPY 1m incl. 2024-11-29 half day | 1pm close; `HasNextCandle` loop, VWAP session length |
| SPY 1h, one month | hourly candle rebuild from minutes, `MergePreviousPeriod(hour)` |
| NVDA 1m | pre-market bars present in Massive response — session-open detection, VWAP anchor |

Fetch with `adjusted=true`, `sort=asc`, `limit=50000`, and include extended hours for the
1-minute sets (that is what `IMarketCache.Initialize` receives). Record the exact query in
`manifest.json`.

## Layer 1 — Golden indicator values

### 1a. `compute_reference.py`

For every bars fixture, compute and emit per-bar values (aligned by `t` timestamp, `null` during
warm-up) for the functions in `MarketViewer.Filters/Functions`:

| DSL | Reference | Notes |
|---|---|---|
| `sma(n)` | `close.rolling(n).mean()` | n ∈ {5, 20, 50, 200} |
| `ema(n)` | hand-rolled loop: SMA seed at bar n−1, then α = 2/(n+1) | **not** pandas `ewm` default (first-close seed) — see 1c |
| `rsi(n, _, _, wilders)` | Wilder's smoothing (`ta.momentum.RSIIndicator` / `pandas_ta.rsi`) | default type in `RsiFunction.cs`; SMA seed + α = 1/n |
| `rsi(n, _, _, ema)` / `sma` | explicit EMA/SMA gain-loss variants | implement inline in the script (few lines) |
| `macd(12,26,9,ema)` | `.value`, `.signal`, `.histogram` | |
| `adv(n)` | `volume.rolling(n).mean()` | **shifted so it excludes the current bar if that is the C# contract — check `AdvFunction.cs` and document** |
| `slope(close, n)` | least-squares slope over last n closes | matches `SlopeFunction.cs` definition; document formula |
| `vwap()` / `vwap(day)` | session VWAP Σ(vw·v)/Σv reset when a bar's span first ends after 09:30 ET on its date, carried through after-hours/pre-market until the next open; `day` resets at the ET date change | `VwapFunction.cs`; bare `vwap` literal removed 2026-08-16 |
| `support_resistance` | *skip* — heuristic, no canonical reference | covered by layer 2 only |

Also emit **aggregated series** for the 1m fixtures: 5m, 15m, 1h and 1d candles built by
pandas `resample` with ET session alignment (`origin` at 09:30, label=left), so layer 3 can
compare the C# candle rebuild against them.

Output shape (one file per fixture):

```jsonc
{
  "source": "AAPL_1m_2025-06-02_2025-06-06.json",
  "generatedBy": "compute_reference.py@<git sha>", "libs": {"pandas": "…", "ta": "…"},
  "series": {
    "sma(20)":   [null, null, …, 201.31, …],
    "rsi(14,70,30,wilders)": [...],
    "macd(12,26,9,ema).histogram": [...],
    ...
  },
  "aggregates": { "5m": [ {t,o,h,l,c,v}, … ], "1d": [...] }
}
```

Keys are literal DSL fragments so the C# test can evaluate the *same string* — no mapping table.

### 1b. `GoldenIndicatorTests.cs`

`[Theory]` over `(fixture, dslKey)` from the manifest. For each, evaluate the series via the
engine and compare bar-by-bar against `reference.series[dslKey]`.

The engine currently exposes only boolean evaluation (`EvaluateScript`, `Evaluate`,
`EvaluateIncremental`). Add a small **internal** hook (`InternalsVisibleTo` the test project)
that returns the planned series for a `FunctionCallExpression`/`FieldAccessExpression` — e.g.
`IndicatorExpressionEngine.EvaluateSeries(string script, StocksResponse, Timeframe) →
IReadOnlyList<float?>`. Plan 05 (entry snapshot) needs the same accessor, so build it once.

Assert three things per series:

1. **Warm-up length** — first non-null index matches the reference (catches off-by-one
   window bugs like the `adv()` one directly).
2. **Values** — `Math.Abs(actual - expected) <= tol` for every non-null bar (§1c).
3. **Incremental == full** — evaluate the same series through `FilterSession.EvaluateIncremental`
   appending one bar at a time from index 200 on; last value must equal the full evaluation.
   (Generalises the existing `Session_Incremental_Yields_Same_Result_As_Full` to real data.)

### 1c. Tolerances and seeds

- Bars are `float` in `Massive.Client.Models.Bar`; the reference is float64. Use relative
  tolerance `1e-4` for sma/adv/vwap, `1e-3` for ema/macd/rsi (recursive, error accumulates),
  absolute `1e-6` floor.
- **Seeds (verified in code 2026-08-16 — the C# conventions are the contract; the reference is
  written to match them, not the other way round):**
  - `ema(n)`: SMA seed — `ema[n-1] = mean(close[0..n-1])`, then `α = 2/(n+1)`
    (`EmaFunction.cs:33,40`). Matches TA-Lib/TradingView. **Not** pandas `ewm(span=n)` default,
    which seeds with `close[0]` and would disagree for hundreds of bars on `ema(200)`; the script
    must implement the loop by hand (~5 lines).
  - `rsi(n,…,wilders)`: SMA seed of first n gains/losses, then `avg = (avg·(n−1)+x)/n`
    (α = 1/n) (`RsiFunction.cs:51-68,112-113`). Matches `pandas_ta.rsi` / `ta.momentum.RSIIndicator`.
  - `rsi(n,…,ema)`: SMA seed, then α = 2/(n+1) on gains/losses (`RsiFunction.cs:86,116`) —
    non-standard combination, no library computes it; implement by hand and comment in both files.
  - `rsi(n,…,sma)`: plain rolling means (`RsiFunction.cs:128-129`).
  - Layer 1 asserts the first non-null index (`n−1` for ema/sma, `n` for rsi) as well as the
    values — the warm-up length is where off-by-one window bugs show up.
- Any deliberate deviation from the standard definition gets a comment in the C# function *and*
  in `compute_reference.py`; the golden test is the executable form of that contract.

## Layer 2 — Golden filter outcomes (regression net)

### 2a. `outcomes/filters.json`

A committed list of ~25 filter scripts, each with the fixture(s) it runs against and the
blessed list of bar timestamps where the filter is `true`:

```jsonc
[
  { "id": "rsi-oversold-5m",
    "script": "rsi(14) < 30 [5m, 3]",
    "fixture": "AAPL_1m_2025-06-02_2025-06-06",
    "trueAt": [1748872200000, …],
    "knownBug": null },
  { "id": "and-or-grouping",
    "script": "close > sma(20) AND (rsi(14) < 30 OR rsi(14) > 70)",
    "fixture": "AAPL_1m_2025-06-02_2025-06-06",
    "trueAt": [...],
    "knownBug": "plan-11: parser has no grouping; expected list computed by hand from reference series" }
]
```

Script set must cover: every function name; every comparison operator; `AND`/`OR`/`NOT`;
`[tf]`, `[tf, n]`, `[, n]` range forms and each `RangeEvaluationMode`; dot-field access
(`macd(…).histogram`, `.signal`); `crosses_over`/`crosses_under` with a series on both sides;
literal-vs-series and series-vs-series comparisons; a scalar-only filter (`float < 50000000`);
`vwap`; a 1d filter that needs previous-period history (`close > sma(200) [1d]`).

### 2b. `GoldenFilterOutcomeTests.cs`

Drives the fixture through the **same loop shape as `ScannerService.GetResultsFromFilter`**:
seed the `StocksResponse` up to the first session open, then for each subsequent minute bar
call `UpdateLatestCandle` + `EvaluateIncremental(evaluationTime: …)` and record timestamps
where the result is `true`. Compare to `trueAt` as sets, reporting the symmetric difference
in the failure message. Cases with `knownBug != null` are `[Fact(Skip = …)]`-style until fixed
(xunit: use a `GoldenTheoryAttribute` that reads the flag) so they show up as *skipped*, not
green.

Blessing: when `GOLDEN_UPDATE=1`, the test rewrites `filters.json` in place with the observed
`trueAt` and fails with a message telling you to review the diff. Initial bless happens
per-case only after the author has spot-checked ≥3 true timestamps against the layer-1
reference series by hand (note that in the PR).

## Layer 3 — Session boundaries + multi-timeframe through the backtester path

### 3a. Candle rebuild vs pandas resample (filters test project)

For each 1m fixture and each of 5m/15m/1h/1d: build candles the way the Lambda does
(`IMarketCache.Initialize` → cached minute response → the candle rebuild in `DataCache.Setup`;
extract the rebuild into a pure static helper if it isn't already so it's callable without S3)
and compare o/h/l/c/v and bar count against `reference.aggregates`. This is where the DST,
half-day and pre-market fixtures do their work: bucket boundaries at 09:30 ET, last bucket on
the half day ends 13:00, and pre-market minutes land in (or are excluded from) the buckets the
same way the reference chose — decide and document which.

### 3b. `MergePreviousPeriod` + `UpdateLatestCandle` (Backtest.Lambda tests)

Extend `DataCacheMergeUnitTests` with the AAPL/NVDA 1d fixtures: merge a "previous year"
response into a "current day" response and assert (a) no duplicate timestamps, (b) the last bar
of the previous period is dropped **only** when it overlaps the current day (the 2026-08-06
bug), (c) `sma(200) [1d]` evaluates to a value matching the layer-1 reference for the current
day. Then simulate one full session with `UpdateLatestCandle` per minute and assert the forming
1d/1h candle's o/h/l/c/v after each update matches a straightforward re-aggregation of the
minutes seen so far.

### 3c. `GoldenScannerTests.cs`

Wire `ScannerService` with a fake `IMarketCache` fed from the fixtures (no S3, no Massive) and
run `GetResultsFromFilter` for 3–4 scripts from `filters.json`; assert the `StrategyEntry`
timestamps equal the layer-2 `trueAt` set. This closes the gap between "engine is right" and
"the Lambda produces the right entries" — the loop bounds, `HasNextCandle` gating and
`evaluationTime` all live here.

## Tooling

- `tools/golden/fetch_fixtures.py --ticker AAPL --from 2025-06-02 --to 2025-06-06 --tf 1m`
  writes `TestData/Golden/bars/…json` verbatim from Massive's aggregates endpoint and updates
  `manifest.json`. Reads `MASSIVE_API_KEY` from env / `local.env`.
- `tools/golden/compute_reference.py` (no args) regenerates every `reference/*.json` from every
  `bars/*.json`; idempotent; prints library versions.
- `tools/golden/README.md`: the two commands, the tolerance table, and "how to add a fixture".
- CI: golden tests run in the normal `dotnet test`; no Python in CI. Add a CI check that
  `manifest.json` hashes match the fixture files so a fixture can't drift without regenerating
  reference values.

## Phasing

1. **Phase 1 (do first)** — fetch script, AAPL 1m + 1d fixtures, `compute_reference.py` for
   `sma/ema/rsi/adv/macd`, `EvaluateSeries` hook, `GoldenIndicatorTests`. Expected to fail on
   `adv()` immediately; fix `AdvFunction.cs` as part of this phase (plan 11 item).
2. **Phase 2** — remaining fixtures (TSLA DST, SPY half-day, SPY 1h, NVDA), `slope`, `vwap`
   reference, `filters.json` + `GoldenFilterOutcomeTests` with `knownBug` annotations for
   grouping/VWAP.
3. **Phase 3** — layer 3: candle rebuild helper extraction, `DataCacheMergeUnitTests`
   extension, `GoldenScannerTests` with fake `IMarketCache`.
4. **Then** — use the suite as the safety net for the plan-11 parser work (parenthesised
   grouping) and VWAP session fix; flip the `knownBug` cases to live as each is fixed.

## Acceptance

- `dotnet test tests/marketviewer-filters-unit-tests` and `tests/backtest-lambda-unit-tests`
  pass with the golden suite included; skipped-with-reason count equals the number of open
  `knownBug` entries and nothing else.
- Deleting any single fixture file or editing one bar value fails the build/tests (manifest
  hash check + at least one dependent test).
- Introducing the old `adv()` whole-series bug, or the `MergePreviousPeriod` unconditional
  drop, fails a golden test — verify by temporarily reverting each once during phase 1/3.
- `plans/11-strategy-dsl-gaps.md` "Known bugs" section links each item to the golden test
  that guards it.

## Findings (from running the suite)

Fixed in this work (each guarded by a golden test named in parentheses):

- **`adv()` whole-series bug** — returned one point at the last bar; now a rolling volume SMA with
  `Append` (`GoldenIndicatorTests adv(*)`).
- **`NOT` bound to the primary, not the comparison** — `NOT close > sma(20)` threw
  `InvalidCastException`; `NOT` could only negate boolean function calls. Parser now takes a
  comparison as the operand (`GoldenFilterOutcomeTests not-unary`).
- **Mixed series operand types crashed comparisons** — a data-access/indicator series
  (`List<IIndicatorResult>`) vs a dot-field/transform series (`List<double>`), e.g.
  `close > support_resistance().support` or `close > macd(...).signal`, fell through to
  `Convert.ToDouble(list)`. `RangeEvaluationHelper.NormalizeMixedSeries` now projects
  (`sr-close-above-support-1d`, `close-gt-macd-signal-mixed`).
- **`UpdateLatestCandle` mis-anchored candles after a gap** — a new candle was stamped with the
  arriving minute (16:41) instead of the grid boundary (16:40) so every later candle drifted off
  Massive's boundaries; only liquid names with no missing session minutes were unaffected. Now
  anchored to the previous candle's grid (`GoldenCandleFormingTests`).
- **`UpdateLatestCandle` mutated cached minute bars** — the cached `NextCandlesCache` bar object was
  added as the forming 5m/1h/1d candle and then merged into in place, corrupting the 09:30, 09:35 …
  minutes for concurrent `[1m]` filters (filters scan in parallel `Task.Run`s) and for downstream fill
  pricing. New candles are now clones (`GoldenScannerTests Scanning_A_Larger_Timeframe_Filter_Must_Not_Mutate…`).
- **MACD warm-up placeholders** — `Signal=0/Histogram=0` points before the signal was seeded made
  band filters (`histogram < 0.5`), `.value > .signal` and `crosses_over(value, signal)` fire
  spuriously in the first 8 bars; points now start when the signal does (`GoldenIndicatorTests macd(*)`).
- **Parenthesised grouping** — was a hard parse error; now supported end-to-end. Also found that the
  parser ignored trailing tokens after the first unparseable one (`close > sma(50) [1d] AND rsi(14) < 30 [1m]`
  "validated" as `close > sma(50) [1m]`); it now rejects them (`ParserGroupingUnitTests`,
  `FilterValidateHandlerUnitTests`).
- **`vwap()` indicator** replaces the per-bar `vw` literal (see open-items list for the design note).
- **`DataCache` overlap rebuild extracted** to `RebuildOverlappingCandle` (pure, static) so it is testable.

Still open (documented, not fixed here):

- ~~MACD signal/histogram warm-up placeholders~~ — **fixed 2026-08-16**: no `MacdResult` is emitted
  before the signal is seeded (first point at bar `slow+signal−2`, all fields real); `Append`
  rewritten (SMA-type signal recomputed from price, EMA/Wilders by recurrence); reference now nulls
  `.value` until the same bar; `WarmupPlaceholderKeys` removed; `wilders` MACD type added to the reference.
- ~~No parenthesised logical grouping~~ — **fixed 2026-08-16** (parser group + leftover-token
  check, `unary` AST node, web round-trip). `and-or-grouped` un-flagged; `or-and-grouped-left/right`,
  `nested-groups`, `not-grouped`, `not-then-and`, `group-with-range` added. No `known_bug` cases remain
  (the theory runs a no-op sentinel when the list is empty).
- ~~`vwap` in the DSL is per-bar `vw`, not session VWAP~~ — **replaced 2026-08-16** by the `vwap()`
  indicator (session-anchored, `vwap(day)` variant); the bare literal is gone. Reference keys `vwap()` /
  `vwap(day)`; outcome cases `close-gt-vwap`, `cross-over-vwap-r3`, `close-lt-vwap-day`, `vwap-vs-sma`,
  `close-gt-vwap-1h`. Design note: the first cut left pre-market bars without a point and the 1h outcome
  case caught that comparison operators right-align by *position*, so a series that stops before the
  last bar compares against a stale point — the session now carries into pre-market instead (contiguous).
- **Backtest 1-minute history is same-day only** (per-day minute file): `sma(200) [1m]` warms up on
  the scan date's pre-market. Live scans have multi-day minute history — a parity gap for plan 10.
  `GoldenScannerTests.Scanner_1m_Filter_Sees_Only_The_Scan_Dates_Minutes_As_History` pins the current behaviour.
- ~~**`ScannerService` never evaluates the 15:59 bar** (`i < totalMinutes - 1`)~~ — RESOLVED 2026-08-17, see
  follow-up 4: the scanner now evaluates all 390 session minutes.
- **`Bar.Volume` is float32** — daily/hourly volume sums above 2^24 lose integer precision (tolerated
  in `GoldenCandleFormingTests`). Cosmetic for filters, but `volume > adv()` on mega-caps compares
  rounded numbers.
- Reference must feed float32-rounded inputs; `slope(ema(20),10)` needs 1e-3, raw-price slope 1e-4.

## Follow-ups (TODO, recorded 2026-08-16 — not yet addressed)

Ordered by my read of impact. Each has enough context to be picked up cold.

1. ~~**Stale incremental indicator values on a forming candle (correctness, multi-timeframe backtests).**~~
   **DONE 2026-08-17.** Root cause was as described, plus two things the note missed: (a) the plain
   `close`/`high`/`low`/`volume` data-access path and the `.value/.histogram` field-access path in
   `FilterSession` also only appended, so `close > sma(20) [5m]` compared a *stale close* against a
   stale SMA; (b) a node is not evaluated on every minute — AND/OR short-circuit skips branches — so
   by the time it runs again its last cached point can be for a bar that has since finished forming
   and is *no longer last*. "Recompute the last point if it is for the last bar" therefore drifts
   (found by the OR filter in the new backtester test); the rule that holds is **always recompute the
   last cached point** — only it can ever be provisional (VwapFunction already did this, which is
   why it never showed the bug). Implemented in `Functions/IncrementalSeries.cs`
   (`ReusablePointCount`/`Seed`), used by `Sma/Ema/Rsi/Macd/Adv/Slope.Append`; `FilterSession.
   EvaluateDataAccess/EvaluateFieldAccess` re-read their last cached element; contract documented on
   `IIncrementalSeriesFunction`. Cost is O(period) per evaluation. Tests: `GoldenIndicatorTests.
   Incremental_Matches_Full_When_Last_Bar_Is_Mutated_In_Place` (every fixture × series; grows each
   bar through 3 provisional shapes, skips the final-shape evaluation on every third bar, compares
   the whole final series — 189/203 cases failed before the fix, 182 under the "only if last bar"
   rule) and `Backtest.Lambda.UnitTests.Golden.GoldenIncrementalFormingTests` (7 minute fixtures ×
   5m/15m/1h through the real `UpdateLatestCandle`, 18 series scripts + 6 whole filters, incremental
   == fresh full evaluation after every session minute). Note: multi-timeframe backtests run before
   this fix evaluated indicators (and `close`) at the candle's *first-minute* values.
2. ~~**Stored strategies that use removed/changed DSL will now fail or differ.**~~ **Not needed
   (2026-08-17):** no stored strategies or backtests use `NOT`, parentheses or `vwap` yet, so there is
   nothing to migrate; `adv()`/MACD warm-up changes only affect results computed before 2026-08-16.
3. ~~**Backtest 1-minute history is same-day only.**~~ **DONE 2026-08-17 — load the previous session.**
   `DataCache.Setup` now downloads the prior session's per-day minute file and prepends it via
   `MergePreviousPeriod` (same dedupe-by-timestamp path as hour/day), so `[1m]` indicators are warm at
   09:30. Decisions: (a) `DataCache.PreviousMinuteSessions = 1`, a constant not a config knob — the live
   cache holds 5 sessions (`MarketCacheWarmer.MinuteFileCount`) but the 3GB worker clones every response
   and a per-day minute file is ~185MB of JSON, so 5 is not affordable; one full extended-hours session
   (~960 bars) covers any realistic `[1m]` warm-up. Residual live/backtest difference: EMA-style
   indicators seeded from 5 vs 2 sessions of history differ slightly. (b) "Previous session" =
   walk back up to 10 calendar days skipping weekends and dates whose file 404s (holidays,
   pre-bucket); a non-404 S3 error fails `Setup` (loud) rather than silently scanning with less
   history — nondeterminism lesson from plan 14 findings. (c) The S3 filter-entry cache key gained a
   version segment (`strategyEntries/v2/...`, `ScannerService.CacheVersion`) so results computed under
   same-day-only history are never reused; bump it with any future change to `PreviousMinuteSessions`.
   Tests: `GoldenScannerTests.Minute_History_Is_The_Previous_Session_Plus_The_Scan_Dates_PreMarket`,
   `Scanner_1m_Filter_Warms_Up_On_The_Previous_Sessions_Minutes` (replaces the pinning test; `sma(400)
   [1m]` fires at 09:30 only with history), `DataCachePreviousMinuteSessionsUnitTests` (weekend, holiday
   404, none-in-lookback, idempotent warm container, non-404 fails). `PreloadedMarketCache.Initialize`
   now throws the same NotFound `AmazonS3Exception` as S3. Deploy note: worker memory rises by roughly
   one minute file; watch the REPORT max-memory line on the first multi-day backtest after deploy.
4. ~~**`ScannerService` never evaluates the 15:59 bar** (`i < totalMinutes - 1`).~~ **DONE 2026-08-17.**
   Decided: not intentional — a filter may legitimately buy in the last minute of the day. Both per-ticker
   loops in `GetResultsFromFilter` (scalar and incremental) now iterate `[0, 390)`; the pinning test and the
   `Replay`/sma(200) helpers in `GoldenScannerTests` were widened to 390. Downstream a 15:59 signal executes
   at 16:00: `WorkerFunction` prices it at the 15:59 close and holds it from the next session's 09:30 bar
   (or drops it via `BuildEntryResult` → null when the timed exit ends the same day). Live matched
   the same day: `ScannerJob.CloseBuffer` (2 min) and the `closingBuffer` parameter on
   `MarketCalendarService.IsMarketOpen` were removed, so live scans run right up to the session close.
5. ~~**Comparison operators align by position, not timestamp.**~~ **DONE 2026-08-17 — enforce the invariant,
   loud, at the producer.** Decisions: (a) keep positional pairing (timestamp alignment is impossible for the
   bare `List<double>` operands — `.signal`, `slope()`, mixed-series normalization — without re-typing every
   series path, and would add per-eval allocations on the backtest hot path); instead every timestamped
   series must be right-aligned with the context bars for the tail a comparison can touch: the last
   `min(range, count)` points, checked point-by-point against `bars[^k]` (O(range), covers `[tf, r]` gaps,
   not just "ends at the last bar"). Warm-up (starting late) and empty series are legal. (b) It throws
   `InvalidOperationException` naming the producer — a violation is a function bug, not user/data error, and
   plan 14 exists because silent fallbacks hid exactly this class of drift. (c) Producer side, one helper
   (`MarketViewer.Filters/SeriesAlignment.AssertTail`), on both evaluation paths:
   `FunctionCallExpression.Evaluate` + `DataAccessExpression.Evaluate` (direct/live) and
   `FilterSession.EvaluateFunctionCall/EvaluateDataAccess` (compiled/incremental, i.e. after `Append`).
   Bare double series inherit the guarantee from the timestamped series they derive from; `time` is exempt
   (single point stamped with the evaluation clock, not a bar). All current producers stamp `bar.Timestamp`
   per bar (sma/ema/rsi/macd/adv/vwap/support_resistance/data access), so the check only fires on a
   regression. Tests: `SeriesAlignmentUnitTests` (helper: warm-up/empty pass, stops-early throws, interior
   gap caught iff inside range; direct path stub function throws / aligned passes; FilterSession full +
   incremental throw; real indicators stay aligned through incremental growth + forming-bar mutation; `time`
   exempt) plus every existing golden/incremental test now runs under the check.
6. ~~**AND/OR precedence.**~~ **DONE 2026-08-17 — standard AND-over-OR.** `a OR b AND c` == `a OR (b AND c)`
   (`ExpressionParser.ParseExpression` → `ParseOr` → `ParseAnd` → `ParseComparison`; NOT still binds to the
   single comparison/call after it; parentheses group). No migration: no stored strategy or backtest filter
   uses AND or OR at all (user-confirmed), so no stored meaning changed. Note the old pinning case
   `a AND b OR c` evaluates identically under both rules; it was renamed `and-then-or` and two real witnesses
   added: `or-then-and-precedence` (`a OR b AND c`) and `or-and-or-precedence` (`a OR b AND c OR d`), plus
   `ParserGroupingUnitTests.Unparenthesised_Or_And_Binds_And_Tighter` and new theory rows. Python reference
   header, filters README, and web `serializeOperand` comment updated (the web serializer still wraps an AND
   under an OR — redundant for the parser now, kept for readability).
7. ~~**Forming-candle `Vwap` is a typical-price approximation.**~~ **DONE 2026-08-17 — volume-weighted, all
   five sites.** The approximation was wider than noted: besides the backtester's `UpdateLatestCandle` and
   `RebuildOverlappingCandle`, the live API used it in `BarCacheService.MergeIntoLastCandle` (forming candle,
   from the *minute's* c/h/l), `MarketCacheWarmer.AggregateBars` (today's hour/day bars built from minutes —
   *completed* candles, so live `vwap() [1h]` read typical price for every intraday hour) and the
   `ScanHandler` live 1h merge. Decision: one shared helper, `Massive.Client.Models.BarVwap`
   (`Merge`/`Aggregate`/`TypicalPrice`), used by all five so live and backtest agree. No new state on `Bar`:
   merging is exact as a recurrence, `(vw₁·v₁ + vw₂·v₂)/(v₁+v₂)` in double, since a candle's VWAP × volume is
   its price·volume sum (`Merge` must run *before* the volume is summed — commented at each site).
   Zero-volume incoming bar leaves VWAP unchanged; no volume at all falls back to typical price (same
   fallback `VwapFunction.MakePoint` applies). Cost: float32 re-rounding per merge drifts ~2e-6 relative over
   a full day into one daily candle (vs ~1e-3 for typical price). Tests: `GoldenData.Aggregate` now carries
   Σ(vw·v)/Σv and `GoldenCandleFormingTests.AssertSameCandle` checks `Vwap` (rel 1e-5) for every forming step
   and rebuilt candle across all minute fixtures × 5m/15m/1h/1d; `BarVwapUnitTests` (weighting, recurrence ==
   aggregate == double definition over 390 random minutes, zero-volume, fallbacks); the two
   `StocksResponseExtensionUnitTests` that pinned `(c+h+l)/3` now pin the weighted value. Not done: no
   `ScannerService.CacheVersion` bump — cached filter entries only differ for `vwap()` filters on 5m+ and no
   stored strategy uses `vwap()` yet; bump if one appears that predates this. Deploy note: the API's S3
   bulk-warmup snapshot holds hour/day bars aggregated with the old formula until the next 3:30am rebuild.
8. ~~**`Studies/VWAP.cs` (chart study, UTC-midnight reset)** is now only reached if `VwapFunction` throws.
   Delete it or make it delegate; the plan-11 UTC-midnight bug note is otherwise moot.~~ **Done 2026-08-17 —
   the whole `MarketViewer.Studies` package and its unit-test project were deleted.** Audit showed its only
   production caller was the `/stocks` fallback in `IndicatorCalculationService`; `/scan`, the backtester and
   the live scanner never touched it, and `Backtest.Lambda`/`Core`/`Infrastructure` carried dead
   `ProjectReference`s (removed, plus the Dockerfile copy lines). `IndicatorCalculationService` now computes
   solely via Filters functions and returns null (indicator omitted, warning logged) when a function throws.
   Decisions from the grilling: `rvol`/`mamr` — chart-only, unreachable from the UI — were dropped along with
   their `StudyType` members (a `rvol(...)` indicator string now fails deserialization → 400 rather than being
   silently omitted); the plan-11 `rvol()` DSL keyword will be written fresh as a Filters function. The dead
   `rvol` option in `StudyOperandForm.tsx` was removed. `StocksHandlerUnitTests` no longer borrows `StudyFixture`.
9. ~~**`Bar.Volume` is float32**~~ — **DONE 2026-08-17.** `Bar.Volume` → `double` (plus
   `MassiveWebsocketAggregateResponse.Volume/AccumulatedVolume`, `StocksResponse.Information.DailyVolume/
   AverageVolume`, `ScanResponse.Item.Volume`, `BarWithTickerConverter` reads `GetDouble`). Prices and
   `Vwap` stay float32 deliberately: the API cache holds ~20M bars on a 4GB VPS (~2GB peak, doubling at
   the 3:30am rebuild), all-double is +43% per bar vs +14% for volume alone, and every consumer already
   computes in double so float32 prices (7 sig. digits, sub-penny below ~$10k) lose nothing that
   matters. Backward compatible with existing S3 JSON (STJ writes float32 as plain digits, double reads
   them). `GoldenCandleFormingTests` volume assertion is now exact; the Python golden reference casts
   only prices to float32 (`compute_reference.py`, `compute_outcomes.py`) and reference/*.json were
   regenerated — `adv()` moved ~6e-9 relative, no filter outcome flipped. Revisit all-double only after
   the API cache memory work (incremental minute merge / struct bars) if uniformity is wanted.
10. ~~**`PerformanceTests` (filters project) is wall-clock based**~~ — **DONE 2026-08-17.** Kept, not
    deleted: the four `*_Incremental_Is_Faster_Than_Full_Recompute` tests guard the one property no
    correctness test can (an `Append` that silently recomputes the whole series is still *correct*).
    Hardened instead: `TestMacdFieldAccess` (asserted nothing, 5 s) deleted; both paths JIT-warmed
    untimed; `Stopwatch` ticks not ms; 2000 initial bars × 2000 appends so full recompute is plainly
    quadratic; assert `full > 2× incremental` (measured 34–82×); class tagged
    `[Trait("Category","Performance")]` — `dotnet test --filter Category!=Performance` for a fully
    deterministic run. Bonus finding: the hardened slope test measured only 4.2× because
    `SlopeFunction.Append` projected the whole input series via `Select(...).ToList()` every call
    (O(n)); now indexes lazily → 34×. Class total ~4 s faster than before.
11. ~~**`macd(...).value` now needs `slow+signal-1` bars**~~ — **DONE 2026-08-17.** Audited: no product
    surface documented the warm-up length (`apps/web/src/config/indicators.ts` holds only params/colors;
    docs/ADRs silent; nothing computes a required-bars figure — DataCache's previous-session preload
    covers it). Added the warm-up fact to the one user-facing DSL help surface, the `macd` entry in
    `FilterValidateHandler`'s function catalog (`/functions` autocomplete).

## Out of scope

- ~~Studies package (`MarketViewer.Studies`) beyond VWAP as consumed by filters.~~ (package deleted, see follow-up #8)
- Live/`ScanHandler` path in `apps/api` (plan 10 territory) — layer 3c is backtester only.
- Performance benchmarks (`PerformanceTests.cs` stays as is).
- Options/futures data; only equity aggregates.
