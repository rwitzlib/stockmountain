# Use Regular Market Hours by Default

StockMountain evaluates Strategies during regular US market hours by default. Extended-hours data may be supported as an explicit Market Session Scope setting when available, but it is not silently included.

**Consequences**

Backtest Runs and Paper Bots must carry Market Session Scope as part of their run configuration. Results should make the selected session scope visible because it can materially change Signals, Trades, and performance.
