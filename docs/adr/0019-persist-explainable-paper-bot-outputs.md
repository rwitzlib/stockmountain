# Persist Explainable Paper Bot Outputs

Paper Bots persist Signals, Rejected Signals, Trades, current Portfolio state, and performance history. This keeps live simulated execution explainable and comparable to Backtest Run results.

**Consequences**

Paper Bot storage and APIs must support historical inspection, not just current bot status. The Paper Bot Runner should record why Signals did or did not become Trades.
