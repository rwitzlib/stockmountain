# Use Redis Streams for Live Bars

StockMountain uses Redis Streams as the first internal distribution mechanism for live Normalized Bars. Redis Streams provide low-latency delivery, consumer groups, and limited replay for restarting consumers without taking on the operational weight of Kafka or Kinesis in the first version.

**Consequences**

The Live Feed Connector publishes live bars to Redis Streams, and internal consumers such as Paper Bot execution and the Backend API realtime hub consume from those streams. Redis may also hold latest-bar cache entries for chart hydration and market data queries.
