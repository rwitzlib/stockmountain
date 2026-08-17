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
| `vwap` (via `DataAccessExpression`) | **per-bar** Massive `vw` field, passed through (`DataAccessExpression.CreateBarResult`) — *not* a session VWAP; reference is identity on `vw` | the session-anchored VWAP lives in `MarketViewer.Studies/VWAP.cs` (chart study, plan-11 UTC-midnight bug) and is out of scope for the filters DSL until a `vwap()` function exists |
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
- **`vwap` in the DSL is per-bar `vw`, not session VWAP** — see 1a table.
- **Backtest 1-minute history is same-day only** (per-day minute file): `sma(200) [1m]` warms up on
  the scan date's pre-market. Live scans have multi-day minute history — a parity gap for plan 10.
  `GoldenScannerTests.Scanner_1m_Filter_Sees_Only_The_Scan_Dates_Minutes_As_History` pins the current behaviour.
- **`ScannerService` never evaluates the 15:59 bar** (`i < totalMinutes - 1`); pinned by
  `Scanner_Emits_An_Entry_For_Every_Session_Minute…`. Intentional? Decide in plan 10/11.
- **`Bar.Volume` is float32** — daily/hourly volume sums above 2^24 lose integer precision (tolerated
  in `GoldenCandleFormingTests`). Cosmetic for filters, but `volume > adv()` on mega-caps compares
  rounded numbers.
- Reference must feed float32-rounded inputs; `slope(ema(20),10)` needs 1e-3, raw-price slope 1e-4.

## Out of scope

- Studies package (`MarketViewer.Studies`) beyond VWAP as consumed by filters.
- Live/`ScanHandler` path in `apps/api` (plan 10 territory) — layer 3c is backtester only.
- Performance benchmarks (`PerformanceTests.cs` stays as is).
- Options/futures data; only equity aggregates.
