# Use a Monorepo for V1

StockMountain uses a monorepo for the first version, with separate deployable applications and shared packages in one repository. This keeps shared domain contracts, Strategy evaluation logic, market data models, Backtest Work Item contracts, and billing primitives versioned together while the service boundaries are still maturing.

**Consequences**

Services should remain independently deployable even though they live in one repository. Separate repositories can be introduced later if ownership, release cadence, security, or tooling needs justify the split.
