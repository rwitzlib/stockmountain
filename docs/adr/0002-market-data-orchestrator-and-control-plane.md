# ADR 0002: Market Data Orchestrator And Control Plane

## Status

Accepted

## Context

Manual market-data backfills require repeated Lambda invocations and there is no first-class way to see which date/timeframe objects exist, which run created them, or which runs failed.

## Decision

Add two separate responsibilities:

- Market Data Orchestrator Lambda expands a date range and set of timeframes into bounded asynchronous Market Data Aggregator invocations.
- MarketViewer API exposes admin endpoints for inventory, run history, backfill requests, and S3 reconciliation.

S3 remains the data plane for large market data payloads. DynamoDB catalog records become the control plane:

- Inventory records track date, multiplier, timespan, bucket, key, status, object metadata, run id, source, and errors.
- Run records track requested ranges, timeframes, counts, status, source, and timing.

## Consequences

Backfills can be requested through the API instead of manual Lambda calls, and operators can answer what data exists without reading every S3 object. The catalog is intentionally repairable by reconciliation, so S3 remains authoritative for payload existence.

The orchestrator invokes workers asynchronously with a concurrency limit. It records run state but does not change the existing backtest data contract.
