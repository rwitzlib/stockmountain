# Allow One Open Trade per Symbol

In the first version, a Portfolio allows only one open Trade per symbol at a time. Additional Signals for a symbol may be recorded, but they do not become Trades while that Portfolio already has an open Trade for the same symbol.

**Consequences**

StockMountain does not support pyramiding or scaling into an existing symbol position in the first version. This keeps position accounting, exit handling, and result reporting simpler for Backtest Runs and Paper Bots.
