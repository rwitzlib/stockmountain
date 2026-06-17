# Strategies Support Boolean Filter Groups

Strategies support Filter Groups so entry logic and Conditional Exits can combine Strategy Filters with nested AND and OR logic. Each Strategy Filter still has one Timeframe, and Filter Groups compose those filters without attaching Timeframes to individual operands.

**Consequences**

The Strategy DSL and persistence model should represent strategy logic as a boolean tree rather than a flat list of filters. A simple AND-only list may be presented in the UI as a convenience, but it is not the underlying model.
