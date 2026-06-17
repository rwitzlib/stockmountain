# Persist a Structured Strategy Model

StockMountain persists Strategies as a structured model rather than raw DSL text. The first authoring experience should favor a visual Strategy builder, while a text DSL can be added later as an advanced view that parses into the same model.

**Consequences**

The Strategy model should represent Filter Groups, Strategy Filters, Timeframes, Exit Rules, Position Sizing Rules, and other Strategy parts explicitly. Any text DSL is an authoring format, not the canonical persisted source of truth.
