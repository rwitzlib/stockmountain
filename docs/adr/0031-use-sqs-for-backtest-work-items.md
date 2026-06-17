# Use SQS for Backtest Work Items

StockMountain uses AWS SQS for Backtest Work Item queues. SQS works well with AWS Lambda for burst execution while remaining accessible to Railway services and VPS-based Backtest Workers.

**Consequences**

The first version may use separate high-priority and low-priority queues. High-priority Backtest Work Items can be consumed by faster Lambda-based workers and may cost more credits, while low-priority work can be consumed by slower VPS workers. Backtest Priority changes execution speed and cost, not result semantics.
