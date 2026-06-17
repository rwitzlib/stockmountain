# Use SignalR for Frontend Realtime Updates

StockMountain uses SignalR for frontend realtime chart updates in the first version. This fits the .NET backend and React frontend stack while providing reconnect behavior, hub/group subscription patterns, and a path to scale with a backplane later.

**Consequences**

The Backend API hosts the client realtime hub and consumes live Normalized Bars from Redis Streams. React clients subscribe through SignalR rather than connecting directly to Redis, the Live Feed Connector, or the external market data provider.
