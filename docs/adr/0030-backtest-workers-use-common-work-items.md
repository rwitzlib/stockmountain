# Backtest Workers Use Common Work Items

Backtest Run execution uses Backtest Work Items consumed by Backtest Workers, independent of whether the worker runs on AWS Lambda, a VPS, or another runtime. This keeps the execution model portable while allowing high-priority or highly parallel work to use Lambda and lower-priority work to use existing VPS capacity.

**Consequences**

Backtest planning, queueing, worker execution, and result aggregation should be defined around a common work-item contract rather than Lambda-specific behavior. Runtime-specific adapters may exist, but they should not change Backtest Run semantics.
