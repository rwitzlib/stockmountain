# Use Split-Adjusted Bars by Default

StockMountain uses split-adjusted, non-dividend-adjusted Normalized Bars by default for Strategy evaluation and Chart Data in the first version. This keeps historical prices and indicators coherent across stock splits without applying total-return dividend assumptions.

**Consequences**

Market data ingestion must normalize historical bars according to the selected adjustment policy. Backtest Run and chart results should be interpreted as split-adjusted price behavior, not dividend-adjusted total return.
