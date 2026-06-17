# Strategies Own Position Sizing

Strategies own the Position Sizing Rule, while Backtest Runs and Paper Bots own Portfolio Constraints such as starting capital, maximum open positions, and maximum allocation. This keeps reusable trading logic together while still allowing each run to define the Portfolio it is evaluated against.

**Consequences**

The Strategy engine applies the Strategy's Position Sizing Rule and then checks run-level Portfolio Constraints before creating Trades. Comparing the same entry and exit logic with different sizing rules requires either editing the Strategy or creating a Strategy variant.
