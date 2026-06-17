# Use Hybrid Storage for Historical Bars

StockMountain stores durable historical Normalized Bars as Parquet files in object storage and stores application data plus market data catalog metadata in Postgres. This balances large-scale intraday bar storage needs with queryable metadata for users, Strategies, Backtest Runs, Paper Bots, ingestion status, and available market data ranges.

**Consequences**

Backtest workers read bulk historical bars from object storage. The Backend API and ingestion processes use Postgres to discover which symbols, Timeframes, date ranges, and files are available. Parquet is the first historical bar file format because it is compressed, columnar, and better suited to large backtest scans than JSON or CSV.
