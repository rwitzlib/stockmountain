# Model Strategy Versions

StockMountain models Strategy Versions in the first version. A Strategy is a named container, and edits create user-visible Strategy Versions. Backtest Runs and Paper Bots are created from a specific Strategy Version and capture an immutable Strategy Snapshot.

**Consequences**

Users can compare results across Strategy Versions, and Backtest Run APIs should support grouping or filtering results by Strategy and Strategy Version. Strategy Snapshots remain necessary because they preserve the exact run input even if version metadata or related references change later.
