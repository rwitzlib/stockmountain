# Normalized Bar Storage Conventions

StockMountain stores historical Normalized Bars as monthly Parquet files in object storage, with catalog metadata in Postgres. Each stored bar uses a UTC timestamp for the bar-period start, decimal prices, and long volume; Timeframe and adjustment policy belong to the Bar Series identity, not to each bar row. Provider-specific timestamp and session quirks are normalized at ingest time.

When a Backfill imports bars for a calendar month that already has a Parquet file, the ingestor read-merge-writes the monthly file: load existing bars, merge by bar-period start timestamp in UTC (incoming bars win on duplicates), write the full month back, and update catalog metadata. This keeps partial Backfills and re-runs idempotent without multiplying files or pushing merge logic onto Backtest Workers.

Scheduled Ingestion and API-triggered Backfill share the same pipeline in `packages/market-data-ingestion`; `apps/market-data-ingestor` stays a thin CLI host. `packages/market-data` owns `IBarSeriesCatalog` and its Postgres implementation, `IBarObjectStore` for S3 get/put, and `IBarSeriesReader` for historical reads. For a requested date range, the Massive adapter fetches the full window (paginating until complete — Massive caps responses at 50,000 bars per call), partitions bars into monthly buckets, clips to the requested window, and read-merge-writes each affected month.

**Ingest scope and timestamps.** The ingestor stores all sessions Massive returns (pre-market, regular, after-hours). Market Session Scope is applied by callers when serving Chart Data or running Backtest Runs, not at ingest time. Daily split-adjusted bars use Massive `t` converted to UTC, converging on midnight UTC for the assigned trading date. Minute-based timeframes (`1m`, `5m`, `15m`, …) trust Massive bucket `t` and strip sub-minute precision only; `1m` additionally floors to the UTC minute boundary. Daily Backfill uses calendar dates in the Massive aggregates URL; minute-based Backfill uses Unix millisecond timestamps for precise partial-day windows. The first ingest milestone supported daily split-adjusted bars only; Phase 2 extends ingest to daily plus any minute-based split-adjusted timeframe.

**Catalog coverage metadata.** Each catalog row stores `earliest_bar_start` and `latest_bar_start` from the merged Parquet contents at registration time, in addition to monthly `period_start` / `period_end`. `FindGapsAsync` and strict reads use the union of these bar spans across overlapping files. Strict coverage checks are file-span granularity only; holes inside an existing span are a documented limitation until a later verification pass is needed.

**Historical reads.** `IBarSeriesReader` in `packages/market-data` orchestrates catalog lookup, S3 fetch, and range-scoped Parquet reads, returning `IAsyncEnumerable<NormalizedBar>` in ascending timestamp order. `ReadAvailableAsync` streams whatever bars exist in the requested window. `ReadAsync` requires full file-span coverage and fails when gaps overlap the request. The ingestor CLI adds a `read` command (with optional `--strict` and `--limit`) alongside `backfill`; both commands accept `--from` / `--to` as ISO 8601 UTC datetimes.

**Considered Options**

Daily Parquet files per Bar Series would simplify incremental ingest but multiply file and catalog row counts for intraday data without fixing Lambda memory use on their own. Workers should use timestamp-range reads rather than loading entire files into memory. Market Session Scope was considered as part of Bar Series identity but rejected because it is a Backtest Run and Paper Bot configuration applied when reading or evaluating bars, not a property of provider aggregates.

Replacing whole monthly files on each import would waste provider API calls and break partial Backfills. Append-only partial files per month were rejected because they multiply catalog rows and push merge complexity onto every reader. One Massive fetch per calendar month was rejected because it multiplies API calls for multi-month Backfills without benefit.

Regular market open was considered as the daily bar-period start anchor but rejected because it couples daily bar identity to session timezone and complicates cross-timeframe alignment; daily aggregates instead use Massive `t` converted to UTC with endpoint-specific correction when verification shows bar-period end semantics.

Filtering to regular market hours at ingest time was rejected because it would require re-ingesting to support extended-hours Backtest Runs and Chart Data. Applying Market Session Scope inside `IBarSeriesReader` was rejected because it couples storage reads to run configuration before the Backend API exists.

Month-level catalog coverage was rejected for strict reads because partial-month files would falsely appear complete. Parquet timestamp scanning on every strict read was rejected as too expensive for large 1-minute ranges in Phase 2. Flooring minute-based bars to a UTC-midnight `{multiplier}`-minute grid was rejected because Massive already aligns aggregate buckets correctly.

Legacy catalog migration with month-boundary fallbacks was rejected in favor of a clean catalog after the bar-span schema change.

**Consequences**

The `packages/market-data` package owns Normalized Bar types, Bar Series keys, monthly object-key conventions, Parquet schema, range-scoped read helpers, `IBarSeriesCatalog` contracts, Postgres catalog persistence, S3 object storage access, and `IBarSeriesReader`. Backtest Workers and the Backend API read historical bars through the shared reader; they apply Market Session Scope after read. Massive may return bar-period start or end depending on endpoint; the ingestor must normalize to bar-period start in UTC before persistence.

Ingest integration tests use recorded Massive JSON fixtures in CI, optional live smoke tests against Massive for API drift, MiniStack as the S3-compatible stand-in, and Postgres via Testcontainers — no real AWS credentials required in CI. Local development may load secrets from a gitignored `.env` file; deployment uses environment variables.
