# Evaluate Strategies on the Fastest Filter Timeframe

A Strategy with multiple Strategy Filters uses an Evaluation Timeframe to determine when it is evaluated during a run. By default, the Evaluation Timeframe is the fastest Timeframe used by the Strategy's entry filters, and filters on slower Timeframes use their latest available values.

**Consequences**

Strategy Filters attach a Timeframe to the whole filter rather than to individual operands. This allows a Strategy to combine filters such as one-minute momentum and daily trend context while producing Signals on a predictable evaluation clock.
