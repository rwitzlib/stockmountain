# Charts Use Normalized Bars

StockMountain charts use historical and live Normalized Bars rather than a separate provider-specific chart data path. If requested historical Chart Data is missing, the system Backfills the missing bars and serves the chart from stored Normalized Bars.

**Consequences**

Charting, Backtest Runs, and Paper Bots share one canonical market data representation. The first version avoids a chart-only proxy path to the external market data provider.
