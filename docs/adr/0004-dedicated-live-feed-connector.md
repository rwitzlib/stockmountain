# Use a Dedicated Live Feed Connector

StockMountain uses a dedicated Live Feed Connector service to own the external live market data connection and publish live market data internally. This keeps the provider connection independent from Backend API deployments, scaling, and restarts, which is important because the provider allows only one WebSocket connection.

**Consequences**

The Backend API serves client realtime connections but does not connect directly to the external live feed. Paper Bot execution and live charting consume internally published live market data from the connector.
