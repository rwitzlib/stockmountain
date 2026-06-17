# Default Entry Fill Is Next Bar Open

When a Strategy produces a Signal from Completed Bar evaluation, the default Entry Fill for the resulting Trade is the next Bar's open. This avoids assuming the system can both observe a completed bar and fill at that same bar's close.

**Consequences**

Backtest Runs and Paper Bots should make Entry Fill assumptions explicit in results. Slippage and fees are configurable Execution Assumptions with zero defaults in the first version. Strategies that allow In-Progress Bars may need separate live execution semantics, but completed-bar evaluation defaults to next-bar-open fills.
