# Golden filter fixtures

Real Massive bar data + independently computed indicator values that the filter engine is
tested against. Design and rationale: `plans/14-golden-filter-tests.md`.

```
tests/marketviewer-filters-unit-tests/MarketViewer.Filters.UnitTests/
  TestData/Golden/
    manifest.json        provenance + sha256 for every bars file (checked by GoldenManifestTests)
    bars/*.json          verbatim Massive aggregates responses (+ _provenance block)
    reference/*.json     per-DSL-fragment reference series, one file per bars file
    outcomes/            (phase 2) blessed filter-outcome snapshots
  Golden/*.cs            the tests
```

## Setup

```
python -m pip install -r tools/golden/requirements.txt
```

`MASSIVE_TOKEN` must be in the environment or in `local.env` (only for fetching).

## Add or refresh a fixture

```
python tools/golden/fetch_fixtures.py --ticker AAPL --from 2025-06-02 --to 2025-06-06 --tf 1m
python tools/golden/compute_reference.py          # regenerates reference/*.json for every bars file
dotnet test tests/marketviewer-filters-unit-tests
```

Both steps must land in the same commit: `GoldenManifestTests` fails if a bars file's hash
doesn't match `manifest.json`, and `GoldenFixture` throws if a reference's `barCount` doesn't
match its bars file. Fixture names are `TICKER_tf_from_to`; the tests derive the evaluation
timeframe from the `tf` segment.

## What the reference computes

Keys are literal DSL fragments (`sma(20)`, `rsi(14,70,30,wilders)`, `macd(12,26,9,ema).histogram`,
`adv(20)`, `slope(close,5)`, …) so the C# side evaluates the same string via
`IndicatorExpressionEngine.EvaluateSeries` and compares bar-by-bar.

The seeds/smoothing are written to match the C# contract, not library defaults — see the header
of `compute_reference.py`. In particular `ema` is SMA-seeded (TA-Lib style, *not* pandas `ewm`)
and `rsi(...,ema)` is SMA-seeded with α = 2/(n+1). Change the C# and the script together.

Tolerances (relative): 1e-4 for sma/adv/slope-of-price, 1e-3 for ema/macd/rsi and anything
derived from them (float32 bar inputs, recursive accumulation); absolute floor 1e-6.

## Adding a new indicator function

1. Implement it in `MarketViewer.Filters/Functions`.
2. Add a reference implementation to `compute_reference.py` and a key to `compute()`.
3. Re-run `compute_reference.py`; the new key is picked up automatically by
   `GoldenIndicatorTests` (both `Series_Matches_Reference` and `Incremental_Matches_Full`).
