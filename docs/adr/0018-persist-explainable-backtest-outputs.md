# Persist Explainable Backtest Outputs

Backtest Runs persist summary metrics, Trades, Signals, and Rejected Signals. This gives users enough detail to understand not only performance but also when a Strategy matched and why some matches did not become Trades.

**Consequences**

Backtest Run result APIs must support pagination or export for large result sets. Storage cost is higher than summary-only results, but the system gains better explainability and strategy debugging support.
