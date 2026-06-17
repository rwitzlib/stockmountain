# Credits Are a V1 Concept

StockMountain includes Credits as a first-version concept for accounting for resource-intensive actions such as Backtest Run execution. Credits are tracked through a Credit Ledger owned by a User Account.

**Consequences**

The first version does not need a full subscription billing system, but it should record Credit grants, reservations, and consumption explicitly. Backtest Priority can affect Credit cost, such as high-priority Lambda-backed execution costing more than low-priority VPS-backed execution. Backtest Run submission checks estimated Credit affordability, but the Credit Reservation is created only when required market data is available and execution is ready to start. Backtest Runs settle actual Credit consumption when execution finishes. Backfill does not consume the requesting User Account's Credits because backfilled data becomes reusable platform data for subsequent requests. Paper Bots do not consume Credits while running; Membership covers a defined amount of Paper Bot capacity.
