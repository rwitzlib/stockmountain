# Normalized Bar Storage Conventions

StockMountain stores historical Normalized Bars as monthly Parquet files in object storage, with catalog metadata in Postgres. Each stored bar uses a UTC timestamp for the bar-period start, decimal prices, and long volume; Timeframe and adjustment policy belong to the Bar Series identity, not to each bar row. Provider-specific timestamp and session quirks are normalized at ingest time.

**Considered Options**

Daily Parquet files per Bar Series would simplify incremental ingest but multiply file and catalog row counts for intraday data without fixing Lambda memory use on their own. Workers should use timestamp-range reads rather than loading entire files into memory. Market Session Scope was considered as part of Bar Series identity but rejected because it is a Backtest Run and Paper Bot configuration applied when reading or evaluating bars, not a property of provider aggregates.

**Consequences**

The `packages/market-data` package owns Normalized Bar types, Bar Series keys, monthly object-key conventions, Parquet schema, range-scoped read helpers, and `IBarSeriesCatalog` contracts. Backtest Workers load only the symbol and date range required for their work item. Massive may return bar-period start or end depending on endpoint; the ingestor must normalize to bar-period start in UTC before persistence.
