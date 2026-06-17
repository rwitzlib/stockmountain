# Snapshot Universes for Runs

Backtest Runs and Paper Bots use a Universe Snapshot captured at creation or start time. If the source Watchlist changes later, the existing run continues using its captured set of Supported Securities.

**Consequences**

Run results are reproducible and not affected by later Watchlist edits. Creating or restarting a Paper Bot can capture a new Universe Snapshot when the user wants updated symbols.
