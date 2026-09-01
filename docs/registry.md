# Registries

The single authoritative home for cross-cutting facts: limits, budgets, storage keys,
configuration, error semantics, and credit accounting. Other docs link here; code is the
final arbiter — every row carries its anchor. Add a row **in the same PR** that adds the
fact (the PR template asks). Verified 2026-09-01.

## Limits and budgets

| Fact | Value | Anchor / why it matters |
|---|---|---|
| Lambda sync response cap | 6MB | AWS platform. Exceeding it kills the .NET runtime with a broken-pipe crash, not a clean error — the reason worker results go through S3 (ADR 0005) |
| Worker lambda | 3008MB / 900s | `infra/tf/app/lambda.tf` |
| Orchestrator lambda | 1024MB / 900s | `lambda.tf`. Orchestrator waits synchronously on workers; a huge range risks the 15-min ceiling |
| Market-data lambdas | 4096MB / 900s | `lambda.tf` |
| Worker → Massive concurrency | 750 in-flight (env `MASSIVE_BATCH_SIZE`) | `WorkerFunction.GetBacktestResults` semaphore |
| Orchestrator → worker concurrency | 100 days per batch (env `WORKER_BATCH_SIZE`) | `BacktestWorkerService.GetBacktestResultsFromLambda` |
| Market-data aggregator batch | 250 (env `BATCH_SIZE`) | `lambda.tf` |
| Worker per-day error detail cap | 5 + "and N more" | `WorkerFunction.SummarizeErrors` |
| Backtest record error cap | 25 + "and N more", date-prefixed, distinct | `OrchestratorFunction.CollectWorkerErrors` |
| DataCache footprint | ~185MB JSON per date loaded | comment in `WorkerFunction` finally block; whole-cache compaction each invocation |

## Error propagation & retry budgets

The taxonomy, bottom-up. "Retryable" means a *fresh attempt is made automatically*;
everything else surfaces to the user on the backtest record.

| Failure | Detected as | Retries | Surfaces as |
|---|---|---|---|
| One Massive aggregate call fails | non-OK `Status` (client never throws) | 3 attempts, expo backoff + jitter (`WorkerFunction.GetAggregatesWithRetry`); 400/401/403/404 not retried | dropped signal in `WorkerResponse.Errors` ("candle data unavailable") |
| Ticker with no bars in window | success + empty results | — (legitimate no-data) | silently no trade |
| `RunDay` throws (e.g. market-data setup on a memory-pressured warm container) | `WorkerResultLocation.Error` | orchestrator retries the day, 3 attempts (`BacktestWorkerService.BacktestDay`) | after 3: placeholder day, "Day could not be backtested after 3 attempts" |
| S3 result upload/fetch fails | pointer `Error` / thrown in orchestrator | same 3-attempt day budget | same placeholder |
| Worker crash (OOM, timeout, runtime death) | `InvokeResponse.FunctionError` | same 3-attempt day budget | placeholder, "worker crashed (…)" |
| Every day failed AND zero results | orchestrator check | — | record `Failed`, worker errors attached |
| Insufficient credits (pre-check) | orchestrator estimate vs balance | — | record `Failed` before any compute |
| Credit debit fails at settlement | `TryDebitCredits` false | — | record `Failed` after compute (wide event `failure_reason=credit_settlement_failed`) |
| Orchestrator itself throws | catch-all | — | record `Failed`, generic message; wide event has `error_type` |
| API→orchestrator invoke lost | nothing (fire-and-forget) | — | record stuck `Pending` — see runbook |

Orchestrator wide-event `failure_reason` values: `not_pending`, `insufficient_credits`,
`no_results_in_range`, `all_days_failed`, `credit_settlement_failed`, `unhandled_exception`
(`OrchestratorFunction.cs`).

## Storage registry

### S3 — backtest data bucket (`…-backtest-data`, `s3.tf`)

| Prefix | Writer → reader | Format | Lifecycle |
|---|---|---|---|
| `strategyEntries/v2/{yyyy/MM/dd}/{sha256[..16]}` | worker ↔ worker (filter scan cache) | gzip+base64 JSON `List<StrategyEntry>` | none. **v2 is `ScannerService.CacheVersion` — bump it whenever filter semantics change**, or stale entries poison every later run of the same filter/date |
| `workerResults/{backtestId}/{yyyy-MM-dd}` | worker → orchestrator (per-day handoff, ADR 0005) | gzip+base64 JSON `WorkerResponse` (`WorkerResultStore`) | none yet — expiration rule is the known follow-up; copy the `shares/` rule in `s3.tf` |
| `backtestResults/{userId}/{id}/universe.json`, `…/portfolio.json` | orchestrator → API (`BacktestRepository`) | JSON | none — this is the durable product |
| `shares/{shareId}.json` | API (share links) | JSON | 30-day expiration (`s3.tf`) |

### S3 — market data bucket (`…-market-data-{region}`, contract v1 = ADR 0001)

`tickerdetails/stocks.json`; `backtest/{yyyy}/{MM}/{dd}/aggregate_{n}_minute`;
`backtest/{yyyy}/{MM}/aggregate_{n}_hour`; `backtest/{yyyy}/aggregate_{n}_day`.
Keys built only via `MarketDataStorageContract`.

### DynamoDB (`dynamodb.tf`)

`market_data` (inventory/run catalog), `user`, `strategy`, `trade`, `scan`,
`execution_dedup`, `meta`, `billing_ledger`, `backtest` (record + `RequestDetailsIndex`,
`UserIndex`). Logical names bind through config sections (`BacktestConfig.TableName` etc.).

### SQS (`sqs.tf`)

`backtest_orchestrator`, `backtest_filter`, `strategy_signals` + DLQ — **provisioned, no
code consumers yet** (verified by grep 2026-09-01); reserved for the SQS migration and
plan 08.

## Configuration registry

| Variable | Read by | Default | Meaning |
|---|---|---|---|
| `MASSIVE_TOKEN` | both backtest lambdas, aggregator | — (secret) | Massive API auth |
| `MASSIVE_BATCH_SIZE` | worker | 750 | in-flight Massive request cap |
| `WORKER_BATCH_SIZE` | orchestrator | 100 | concurrent day invocations |
| `BATCH_SIZE` | market-data aggregator | 250 | aggregation batch |
| `MEMORY` | worker (`LambdaEnvironment.GetMemoryFactor`) | falls back to `AWS_LAMBDA_FUNCTION_MEMORY_SIZE`, then 2GB | credit-metering memory factor |
| `COMMIT_SHA` | all lambdas (wide events) | `unknown` | build provenance |
| `ASPNETCORE_ENVIRONMENT` | all | — | environment tag |
| `OTEL_*` | API | — | see `observability.md` |

Non-env config: `BacktestConfig` / `UserConfig` sections in
`apps/backtester/Backtest.Lambda/appsettings.json` (table names, worker `LambdaName`,
`S3BucketName`); the API's equivalents in `apps/api/MarketViewer.Api/appsettings.{env}.json`
(its `BacktestConfig.LambdaName` points at the **orchestrator**, the backtester app's at the
**worker** — same section name, different target, easy to confuse).

## Credits

1 credit = 100 GB-seconds of worker compute (`CreditMeter.GbSecondsPerCredit`).
Pre-flight estimate: `0.35 × calendar days`, ceiling — ~p90 of observed cost, so ~10% of
runs exceed it and rely on the settlement-time debit failure path. Actual usage: per-day
`memory_GB × seconds / 100`, summed over relevant days; failed placeholder days bill 0.
Debit happens once at completion (`TryDebitCredits`); free-tier grants and Stripe top-ups
are plan 16/17 (implemented).
