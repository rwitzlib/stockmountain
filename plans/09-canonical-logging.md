# Plan 09 — Canonical (wide-event) logging + live Lambda cost visibility

## Problem

Two pains, one initiative:

1. **No live cost view.** AWS Cost Explorer lags by hours. There is no way to watch Lambda
   invocations / GB-seconds spend in near-real-time, per function or per backtest.
2. **Unreadable logs.** The backtest lambdas emit ~48 scattered narrative log lines per
   invocation. When many workers run concurrently, Grafana is an interleaved mess that is
   hard to query and impossible to build dashboards on.

The fix is canonical log lines ("wide events" — see `/logging-best-practices` skill and
loggingsucks.com): one context-rich JSON event per Lambda invocation, plus dashboards that
compute live cost from logs already emitted by the Lambda platform.

## Decisions already made (grilling outcomes — do not re-litigate)

1. **Cost source of truth = platform `REPORT` lines**, not wide events. REPORT lines contain
   billing-exact `Billed Duration` and `Memory Size`, are emitted by the platform even when
   the handler times out or OOMs, and require zero code changes. Wide events carry
   *approximate* cost fields for per-backtest/per-user attribution only.
2. **Crash detection**: a `REPORT` line with no matching wide event (join on AWS request id)
   means the invocation died (timeout/OOM). This is a dashboard query, not code.
3. **Scope**: forward **all four deployed lambdas'** log groups to Loki (market-data
   aggregator, market-data orchestrator, backtest worker, backtest orchestrator), **unfiltered**
   — the ingest bill is accepted; revisit only if it hurts. The wide-event code refactor
   applies to the backtest lambdas only for now.
4. **Logging style**: per invocation, exactly one wide event; **delete narrative info-level
   logs**; **keep point-of-failure error logs** (per-ticker/per-day failures), each tagged
   with `backtest_id` so they correlate with the wide event.
5. **Observability only.** Cost numbers do not feed the credits/billing system. Loki's
   best-effort delivery and finite retention are acceptable. No DynamoDB write-back.
6. **Plumb `UserId` through the fan-out**: add `UserId` to `WorkerRequest` (and any other
   backtest fan-out payload) so every wide event carries `backtest_id` + `user_id`. Additive,
   optional field — tolerant of in-flight SQS messages during deploy.
7. **Dashboards as code**: Grafana dashboard JSON + alert rules provisioned from `infra/` via
   the terraform Grafana provider. Lambda pricing constants live as dashboard variables
   (editable in one place; x86 us-east-2: `$0.0000166667` per GB-second, `$0.20` per 1M
   requests as of mid-2026).
8. **Delete dead code**: `DispatcherFunction.cs` and `FilterFunction.cs` are not deployed
   (no terraform) and nothing references them — remove them as part of this work.
   (Verified 2026-07-21: only self-references in the repo; `FilterFunctionName` in
   `apps/web/src/types/filters.ts` is unrelated.)
9. **Bake deploy identity into images**: `COMMIT_SHA` build-arg → env var in all four app
   Dockerfiles, passed from CI (`github.sha` — the `build-args` slot in
   `.github/workflows/app-deploy.yml` is already there, currently empty). Wide events carry
   commit, function name, memory size, region, environment.
10. **Logger home**: wide-event builder/emitter lives in **`MarketViewer.Infrastructure`**
    (already referenced by the backtester) so api/management-api can adopt the identical
    schema later.
11. **Alerting**: provision cost alert rules with the dashboards — daily spend threshold and
    a GB-seconds rate-spike rule — delivered via a Grafana Cloud email contact point.

## Current state (for orientation)

- Handlers: [WorkerFunction.cs](../apps/backtester/Backtest.Lambda/WorkerFunction.cs) (3008 MB)
  and [OrchestratorFunction.cs](../apps/backtester/Backtest.Lambda/OrchestratorFunction.cs)
  (1024 MB), both container-image lambdas, 900 s timeout
  ([lambda.tf](../infra/tf/app/lambda.tf)).
- Log pipeline: CloudWatch → lambda-promtail → Grafana Cloud Loki
  ([observability.tf](../infra/tf/app/observability.tf)). Labels: `service_name` (from log
  group), `environment`. Today only the backtest orchestrator + worker log groups have
  subscription filters.
- `WorkerRequest` already carries `BacktestId`; `UserId` stops at the orchestrator
  (credits check).

## Work breakdown

### Phase A — dead code removal
Delete `DispatcherFunction.cs`, `FilterFunction.cs`, and anything that becomes unreferenced
solely because of that deletion (check `ScannerService` usages afterward — the worker path
may still use it). Build + tests must stay green.

### Phase B — deploy identity
1. Each of the four app Dockerfiles: `ARG COMMIT_SHA` → `ENV COMMIT_SHA=$COMMIT_SHA`.
2. `.github/workflows/app-deploy.yml`: set `"build-args": "COMMIT_SHA=${{ github.sha }}"`
   for each image.

### Phase C — wide-event emitter (MarketViewer.Infrastructure)
A small `WideEvent` type + `WideEventLogger`:
- Collected during the invocation (dictionary-style, handlers add business fields), emitted
  **once** in a `finally` block as a **single JSON line to stdout** (bypass the
  `LambdaLogger` prefix formatting so Loki `| json` parses it cleanly). Include a marker
  field, e.g. `"event":"canonical"`, so queries can select wide events cheaply.
- Base fields populated automatically: `timestamp`, `service`, `function_name`
  (`AWS_LAMBDA_FUNCTION_NAME`), `aws_request_id` (from `ILambdaContext`), `cold_start`
  (static bool flipped on first invoke), `commit` (`COMMIT_SHA`), `memory_mb`
  (`context.MemoryLimitInMB`), `region`, `environment`, `duration_ms`, `outcome`
  (`success` | `error`), `error.type` / `error.message` when thrown.
- Approximate cost fields: `est_gb_seconds` = duration × memory / 1024, `est_cost_usd`.
  Named `est_` deliberately — they exclude cold-start INIT billing (container images bill
  init) and die with the runtime on timeout/OOM; REPORT lines are the exact record.
- Two log levels only (`info` for the wide event, `error` for point-of-failure logs), per
  the logging-best-practices skill.

### Phase D — refactor backtest handlers
- `WorkerRequest` (and orchestrator→worker plumbing): add optional `UserId`.
- `WorkerFunction`: one wide event with business context — `backtest_id`, `user_id`, `date`,
  `entry_count`, `filter_count`, `days_processed`, `retry_attempts`, `error_count`,
  cache hit/miss stats from `DataCache`, elapsed phases (scan ms, compute ms).
- `OrchestratorFunction`: wide event with `backtest_id`, `user_id`, fan-out size, dates
  dispatched, aggregate result counts, credits info.
- Delete narrative `LogInformation` calls; keep `LogError` calls at failure sites and add
  `backtest_id` to each so they join to the wide event.

### Phase E — log forwarding (terraform)
Add subscription filters (empty filter pattern) for the two market-data lambda log groups to
lambda-promtail, mirroring the existing backtest ones. Widen the
`aws_lambda_permission.lambda_promtail_cloudwatch` source ARN if needed (it currently matches
`backtest-*` only).

### Phase F — dashboards + alerts as code (terraform Grafana provider)
New terraform (Grafana Cloud API key required — will need to be provisioned and added to
secrets):
- **Live Lambda cost dashboard**, from REPORT lines. LogQL sketch:
  `{service_name=~".+"} |= "REPORT RequestId" | pattern` → extract `Billed Duration` and
  `Memory Size` → `sum by (service_name)` of GB-seconds rate × price variable; plus
  invocation counts × per-request price. Panels: spend today, spend rate, GB-s by function,
  invocations by function, cold-start init time.
- **Backtest operations dashboard**, from wide events: backtests by user, per-backtest
  est. cost (`sum by (backtest_id)`), duration percentiles, error rates, cache hit rates,
  crash detector (REPORT count minus wide-event count per function).
- **Alert rules**: daily spend > threshold; GB-seconds rate spike vs trailing baseline.
  Contact point: email.

## Ordering / hand-off notes

Phases A and B are independent and can go first in any order. C → D is the core sequence.
E and F are infra-only and independent of C/D, but F's wide-event panels are only testable
after D ships. Suggested PR slicing: (A+B), (C+D), (E), (F).

## Implementation notes (2026-07-21 — all phases implemented, not yet deployed)

- **REPORT records were being suppressed.** The backtest lambdas already had
  `logging_config { log_format = "JSON", system_log_level = "WARN" }` — platform.report
  records are INFO-level system logs, so they never reached CloudWatch. Fixed by setting
  `system_log_level = "INFO"`; the market-data lambdas got the same JSON logging_config so
  the whole fleet parses uniformly.
- JSON log format wraps everything: platform metrics arrive as
  `{"type":"platform.report","record":{"metrics":{...}}}` (parse: `| json |
  type=\`platform.report\``) and wide events arrive wrapped in
  `{"level":...,"message":"<wide event json>"}` (parse: `|= \`canonical\` | json |
  line_format \`{{.message}}\` | json | event=\`canonical\``).
- LogQL cannot multiply two unwrapped fields, so GB-seconds queries multiply per-function
  memory in as a constant — dashboards are generated from terraform (`grafana.tf` builds the
  JSON via `jsonencode`), so the memory values flow from the `aws_lambda_function` resources
  and can never drift.
- Cache stats: `ScannerService.LastScanCacheStats` exposes per-invocation S3 filter-cache
  hits/misses (safe because Lambda runs one invocation per container).
- New terraform variables: `grafana_api_key` (sensitive), `grafana_url`,
  `grafana_loki_datasource_uid` (default `grafanacloud-logs`), `grafana_alert_email`,
  `lambda_price_per_gb_second`, `lambda_price_per_request`, `lambda_daily_cost_alert_usd`.
  Dashboards/alerts are skipped unless both `grafana_url` and `grafana_api_key` are set.
- Alert rules: daily spend > threshold (5-min evaluation), and 1h GB-seconds > 5x the
  trailing-24h hourly average + 100 GB-s floor (15m for). Contact point: email.

## Verification

- After D: run a backtest in dev; confirm exactly one `event=canonical` line per worker
  invocation in Loki, `| json` parses it, `backtest_id`/`user_id` present on every event.
- After E: REPORT lines from all four lambdas visible in Loki.
- After F: cost dashboard total for a day ≈ next-day Cost Explorer Lambda line item (expect
  small drift from free tier / rounding, not double-digit percent).
- Crash check: kill a worker mid-run (or force a timeout in dev) and confirm the crash
  detector panel catches the REPORT-without-wide-event case.
