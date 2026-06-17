# Enforce Usage Policies from V1

StockMountain enforces Usage Policies from the first version, even if paid billing is added later. Market data access, Backtest Runs, Paper Bots, chart subscriptions, and API requests can create infrastructure cost and need explicit limits.

**Consequences**

The Backend API and worker submission paths must check Usage Policy before accepting resource-consuming work. The first version can use one default policy for all User Accounts, but the model should support different limits later.
