# Snapshot Strategies for Runs

Backtest Runs and Paper Bots use a Strategy Snapshot captured at creation or start time. If the source Strategy changes later, the existing run continues using its captured Strategy definition.

**Consequences**

Run results are reproducible and explainable even when users edit Strategies over time. Starting a new Backtest Run or restarting/reconfiguring a Paper Bot can capture a newer Strategy Snapshot.
