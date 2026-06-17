# Share the Strategy Evaluation Engine

Backtest Runs and Paper Bots use the same Strategy evaluation engine so that declarative Strategy logic is interpreted consistently across historical and live simulated execution. The engine is packaged as a shared .NET library rather than a standalone network service. Backtest Runs differ from Paper Bots by data source and clock, not by Strategy semantics.

**Consequences**

The evaluation engine should be packaged behind a reusable boundary that can run inside both backtest workers and paper bot execution. Service-specific code may handle scheduling, data access, status, and persistence, but it must not reimplement Strategy interpretation. Keeping the engine in-process avoids network overhead in performance-sensitive backtests while preserving one implementation of Strategy semantics.
