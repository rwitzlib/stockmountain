# Golden filter fixtures

Real Massive bar data + independently computed indicator values and filter outcomes that the
filter engine and the backtester's data path are tested against. Design and rationale:
`plans/14-golden-filter-tests.md`.

```
tests/marketviewer-filters-unit-tests/MarketViewer.Filters.UnitTests/
  TestData/Golden/
    manifest.json        provenance + sha256 for every bars file (checked by GoldenManifestTests)
    bars/*.json          verbatim Massive aggregates responses (+ _provenance block)
    reference/*.json     per-DSL-fragment reference indicator series, one file per bars file
    outcomes/filters.json  expected true-timestamps per filter script per fixture (layer 2)
  Golden/
    GoldenIndicatorTests      layer 1: every indicator series vs reference (+ incremental == full)
    GoldenFilterOutcomeTests  layer 2: whole filter scripts replayed like the backtester
    GoldenReplay              the replay driver (mirrors compute_outcomes.py's window rules)
    GoldenManifestTests       fixture integrity

tests/backtest-lambda-unit-tests/Backtest.Lambda.UnitTests/Golden/   (fixtures linked via csproj)
    GoldenCandleFormingTests  layer 3a: UpdateLatestCandle / RebuildOverlappingCandle vs clock-aligned aggregation
    GoldenScannerTests        layer 3b/3c: DataCache.Setup + ScannerService end-to-end on a preloaded IMarketCache
    PreloadedMarketCache      the IMarketCache fake (Initialize never touches S3)
```

## Setup

```
python -m pip install -r tools/golden/requirements.txt
```

`MASSIVE_TOKEN` must be in the environment or in `local.env` (only for fetching).

## Add or refresh a fixture

```
python tools/golden/fetch_fixtures.py --ticker AAPL --from 2025-06-02 --to 2025-06-06 --tf 1m
python tools/golden/compute_reference.py    # regenerates reference/*.json for every bars file
python tools/golden/compute_outcomes.py     # regenerates outcomes/filters.json (keeps blessed snapshots)
dotnet test tests/marketviewer-filters-unit-tests
dotnet test tests/backtest-lambda-unit-tests
```

All steps must land in the same commit: `GoldenManifestTests` fails if a bars file's hash doesn't
match `manifest.json`, `GoldenFixture` throws if a reference's `barCount` doesn't match its bars
file, and `GoldenFilterOutcomeTests` checks the replay window size against `evaluatedCount`.
Fixture names are `TICKER_tf_from_to`; the tests derive the evaluation timeframe from `tf`.

## Layer 1 — reference indicator values

Keys are literal DSL fragments (`sma(20)`, `rsi(14,70,30,wilders)`, `macd(12,26,9,ema).histogram`,
`adv(20)`, `vwap()`, `slope(close,5)`, …) so the C# side evaluates the same string via
`IndicatorExpressionEngine.EvaluateSeries` and compares bar-by-bar.

The seeds/smoothing are written to match the C# contract, not library defaults — see the header
of `compute_reference.py`. In particular `ema` is SMA-seeded (TA-Lib style, *not* pandas `ewm`)
and `rsi(...,ema)` is SMA-seeded with α = 2/(n+1). Change the C# and the script together.

Tolerances (relative): 1e-4 for sma/adv/slope-of-price, 1e-3 for ema/macd/rsi and anything
derived from them (float32 bar inputs, recursive accumulation); absolute floor 1e-6.

## Layer 2 — filter outcomes

`compute_outcomes.py` holds the case list (script + a Python predicate over the reference series)
and the evaluator that mirrors the engine's semantics (right-aligned last-`r` bars, `all|any`,
crosses, `time` in ET). Three kinds of case:

- `reference` — expected computed in Python; a mismatch means engine and reference disagree.
- `snapshot` — no reference exists (`support_resistance`). Bless with
  `GOLDEN_UPDATE=1 dotnet test tests/marketviewer-filters-unit-tests --filter GoldenFilterOutcome`;
  the test writes the observed outcome into the *source* `filters.json`. Review the diff.
- `known_bug=...` — expected to fail today. `Known_Bug_Still_Reproduces` asserts it still fails, so
  fixing the bug turns that test red until you drop the annotation and regenerate.

Adding a case: append a `Case(...)` to `CASES`, run `compute_outcomes.py`, run the tests.
Keep 1m cases off `support_resistance` (non-incremental; minutes to replay).

## Adding a new indicator function

1. Implement it in `MarketViewer.Filters/Functions`.
2. Add a reference implementation to `compute_reference.py` and a key to `compute()`.
3. Re-run `compute_reference.py`; the new key is picked up automatically by
   `GoldenIndicatorTests` (both `Series_Matches_Reference` and `Incremental_Matches_Full`).
4. Optionally add outcome cases using it to `compute_outcomes.py`.
