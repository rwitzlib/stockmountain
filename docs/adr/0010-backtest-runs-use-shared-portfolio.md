# Backtest Runs Use a Shared Portfolio by Default

Backtest Runs use a shared Portfolio across the entire Universe by default. Symbol-based worker partitioning can identify Strategy matches in parallel, but Trade creation and performance results must account for shared capital, open positions, position sizing, and other portfolio-level constraints.

**Consequences**

Backtest execution likely needs a merge or portfolio simulation stage after symbol-level evaluation. Treating each symbol independently may be useful as an analysis mode later, but it is not the default Backtest Run semantics.
