# Include Authentication from the Beginning

StockMountain includes authentication and User Account ownership from the beginning. Strategies, Backtest Runs, and Paper Bots are owned by User Accounts rather than being global resources.

**Consequences**

Persistence, APIs, workers, and result storage must carry ownership boundaries from the first version. This avoids retrofitting authorization across core entities later.
