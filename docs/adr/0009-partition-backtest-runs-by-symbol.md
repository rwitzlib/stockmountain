# Partition Backtest Runs by Symbol First

StockMountain Backtest Workers partition Backtest Run execution primarily by symbol in the first version. This keeps each symbol's timeline, indicator state, open positions, and timed exits together while still allowing large Universes to be processed in parallel.

**Consequences**

Very long single-symbol Backtest Runs may not parallelize as well as symbol-heavy runs. Date-range partitioning can be added later if needed, but it must handle indicator warmup, open positions across boundaries, and result merging carefully.
