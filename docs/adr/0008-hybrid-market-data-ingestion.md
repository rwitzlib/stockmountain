# Use Hybrid Market Data Ingestion

StockMountain uses Scheduled Ingestion to keep commonly used Normalized Bars available and Backfill to import missing historical data needed for specific requests. This avoids requiring all possible data to be preloaded while still giving common Backtest Runs a faster path.

**Consequences**

Backtest Run submission must check the market data catalog before execution. If required data is missing, the system queues Backfill work implicitly and delays execution until the needed data is available. Clients can inspect market data coverage, but normal Backtest Run creation does not require a separate data preparation step.
