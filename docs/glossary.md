# Glossary

## Market Data Aggregator

Lambda that retrieves ticker details and historical aggregate bars from Polygon and writes bulk market data objects to S3.

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

Process that checks S3 object metadata and repairs or creates catalog inventory records without re-fetching Polygon data.

## Backfill

Manual or API-triggered request to produce market data for a historical date range and set of timeframes.
