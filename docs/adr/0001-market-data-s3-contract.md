# ADR 0001: Market Data S3 Contract

## Status

Accepted

## Context

Backtests and MarketViewer tools consume historical market data from S3. The producer is the Market Data Aggregator Lambda, but consumers are coupled to bucket names, object keys, and JSON shape rather than the Lambda name.

## Decision

Keep the current bulk-object S3 layout as contract v1:

- Ticker details: `tickerdetails/stocks.json`
- Minute aggregates: `backtest/{yyyy}/{MM}/{dd}/aggregate_{multiplier}_minute`
- Hour aggregates: `backtest/{yyyy}/{MM}/aggregate_{multiplier}_hour`
- Day aggregates: `backtest/{yyyy}/aggregate_{multiplier}_day`

The shared `MarketDataStorageContract` builds these keys for both producers and consumers. Aggregates are serialized as `StocksResponse[]`, which is the type consumers already deserialize and cache.

## Consequences

This keeps existing backtest and API consumers compatible while removing duplicated key builders. We will not split data into per-ticker S3 objects in this pass because current consumers load whole market datasets by date/timeframe, and object fan-out would likely increase S3 request cost and orchestration complexity.

Compressed bulk JSON is the preferred next optimization if object size or JSON parsing becomes a measured bottleneck. Parquet or other columnar formats should wait until consumers need selective reads.
