# Glossary

## Wide Event

One context-rich JSON log line emitted exactly once per lambda invocation from a `finally`
block; the primary diagnostic instrument. Field reference: `docs/observability.md`.

## Worker Result Handoff

The worker's full per-day `WorkerResponse` stored at S3 `workerResults/{backtestId}/{date}`
and returned to the orchestrator as a `WorkerResultLocation` pointer (ADR 0005).

## Filter Cache

Per-(filter, date) scan results cached at S3 `strategyEntries/{CacheVersion}/…`; the
version segment must be bumped whenever filter semantics change.

## Placeholder Day

The `WorkerResponse` the orchestrator fabricates for a day that failed all three worker
attempts, so the failure is visible on the backtest record instead of the day silently
vanishing.

## Execution Minute

A bar timestamped T is only observable once it completes at T+1; trades are stamped and
eligibility is checked on that execution clock, mirroring live behavior (ADR 0003/0004).

## Credit

Billing unit for backtest compute: 100 GB-seconds of worker time (`CreditMeter`).

## Market Data Aggregator

Lambda that retrieves ticker details and historical aggregate bars from Massive and writes bulk market data objects to S3.

## Market Data Orchestrator

Lambda that accepts a backfill range, expands it into trading-day and timeframe work items, and invokes the Market Data Aggregator with bounded concurrency.

## Aggregate File

Bulk S3 object containing aggregate bar data for all available tickers for a date/timeframe contract key.

## Ticker Details File

S3 object at `tickerdetails/stocks.json` containing active stock and ETF ticker details.

## Inventory Record

DynamoDB catalog record describing one expected or existing market data object, including its date, timeframe, S3 key, status, object metadata, and producing run.

## Run Record

DynamoDB catalog record describing a scheduled or manual market data production run.

## Reconciliation

Process that checks S3 object metadata and repairs or creates catalog inventory records without re-fetching Massive data.

## Backfill

Manual or API-triggered request to produce market data for a historical date range and set of timeframes.
