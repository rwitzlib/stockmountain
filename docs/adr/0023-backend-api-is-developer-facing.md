# Backend API Is Internal-Team Facing

StockMountain's Backend API is not a general end-user product surface or an external developer platform in the first version. End users interact with StockMountain through the React frontend, while the StockMountain team may use authenticated internal endpoints for development and app testing.

**Consequences**

The Backend API should expose StockMountain concepts such as Normalized Bars, Strategies, Backtest Runs, and Paper Bots rather than provider-specific proxy endpoints. Product UX for general users belongs in the frontend, and external developer API concerns are deferred beyond V1.
