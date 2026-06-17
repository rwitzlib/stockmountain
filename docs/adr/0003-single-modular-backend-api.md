# Start with a Single Modular Backend API

StockMountain starts with one modular .NET backend API for user-facing contracts, including market data queries, Strategy management, Backtest Run submission and results, Paper Bot management, and realtime client connections. Long-running and specialized processing runs in separate workers or services rather than inside the web API.

**Considered Options**

- Split user-facing capabilities into multiple backend APIs from the beginning.
- Use one modular backend API and separate only the worker-style processing.

**Consequences**

The API project should keep clear module boundaries so capabilities can be extracted later if needed. Background ingestion, live feed connection, backtest execution, and paper bot execution should not depend on web request lifetimes.
