# Use Railway for V1 Application Hosting

StockMountain uses Railway for the first-version frontend, Backend API, Live Feed Connector, and Paper Bot Runner to keep hosting simple and budget-conscious. AWS is used selectively where it fits the workload, such as S3 for historical Parquet storage and potentially Lambda for burst Backtest Run execution.

**Consequences**

The first version is not an all-AWS deployment. Backtest execution may use a mixed model: AWS Lambda for high-parallelism jobs and an existing VPS for slower or lower-priority backtest work. Service boundaries should stay portable enough that workloads can move between Railway, Lambda, VPS workers, or a later cloud platform without changing domain semantics.
