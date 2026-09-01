# Observability

Two halves: **reading the telemetry** (below — start here when diagnosing) and **setting
up the pipelines** (second half). Symptom-driven entry points live in
[runbook.md](runbook.md).

## Reading the telemetry

### Wide events (canonical log lines)

Each backtest lambda invocation emits exactly one JSON line from a `finally` block
(`MarketViewer.Infrastructure/Logging/WideEvent.cs`) — it survives handler failures and,
because it fires *before* the response is posted, even runtime death. It is the primary
instrument: one line answers "what did this invocation do and what did it cost".

Base fields (all services): `event=canonical`, `timestamp`, `service`, `function_name`,
`region`, `environment`, `commit`, `cold_start`, `memory_mb`, `aws_request_id`,
`duration_ms`, `level`, `outcome` (`success`|`error`), `error_type`, `error_message`,
`est_gb_seconds`, `est_cost_usd` (estimates — the platform `REPORT` record is
billing-exact).

`service=backtest-worker` adds: `backtest_id`, `user_id`, `backtest_date`, `filter_count`,
`entry_count`, `scan_ms`, `filter_cache_hits`/`filter_cache_misses`, `result_count`,
`compute_ms`, `dropped_signal_count`, `gate_skipped_count`, `credits_used`, `result_bytes`
(stored S3 payload size — watch this for payload growth).

`service=backtest-orchestrator` adds: `backtest_id`, `user_id`, `backtest_start`/`_end`,
`day_span`, `filter_count`, `estimated_credit_cost`, `available_credits`,
`worker_day_count`, `relevant_day_count`, `fan_out_ms`, `worker_error_count`,
`backtest_status`, `failure_reason` (values listed in
[registry.md](registry.md#error-propagation--retry-budgets)), `credits_used`,
`hold_profit`, `high_profit`.

**Adding a field?** `Set("snake_case", primitive)` in the handler and add it to the list
above in the same PR.

### Correlation keys

Every backtest log line carries structured `BacktestId` (+ `BacktestDate` on workers) via
log scope, and `AwsRequestId`. To walk one incident: record id → orchestrator wide event →
per-day worker wide events (`backtest_id` field) → a crashed invocation's surrounding
platform records (`aws_request_id` ↔ CloudWatch `requestId`).

### Platform records

Lambda JSON logging emits `platform.start` / `platform.report` / `platform.runtimeDone`
records. `report` is the billing-exact source (`billedDurationMs`, `maxMemoryUsedMB` —
the Grafana cost dashboards in `infra/tf/app/grafana.tf` are built on it). A
`runtimeDone` with ERROR followed by a fresh `platform.start` = process crash + cold
start; the crash's stderr lines sit between them.

### Queries that answer common questions

```logql
# Everything about one backtest, both lambdas:
{__aws_cloudwatch_log_group=~"/aws/lambda/stockmountain-dev-backtest-.*"} |= "<backtest-id>"

# Wide events only, e.g. hunting oversized days:
{__aws_cloudwatch_log_group="/aws/lambda/stockmountain-dev-backtest-worker"}
  | json | event="canonical" | result_count > 5000

# Crashed invocations (billing-exact):
{__aws_cloudwatch_log_group="/aws/lambda/stockmountain-dev-backtest-worker"}
  | json | type="platform.report" | record_status="error"
```

---

# Pipeline setup (Grafana logging)

StockMountain uses two log-ingestion paths:

- The API exports logs, metrics, and traces directly to Grafana Cloud over OTLP.
- AWS Lambda logs remain in CloudWatch and are forwarded to Grafana Cloud Logs by Grafana's `lambda-promtail`.

Keeping Lambda output in CloudWatch preserves the normal AWS failure path while avoiding network export and flush work in each backtest invocation.

## API

Set these environment variables on the deployed API:

```text
OTEL_EXPORTER_OTLP_ENDPOINT=https://otlp-gateway-prod-us-east-2.grafana.net/otlp
OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
OTEL_EXPORTER_OTLP_HEADERS=Authorization=Basic <base64(OTLP_INSTANCE_ID:CLOUD_ACCESS_POLICY_TOKEN)>
OTEL_SERVICE_NAME=MarketViewer.Api
OTEL_RESOURCE_ATTRIBUTES=deployment.environment.name=dev
```

Create the token from the OpenTelemetry configuration page in Grafana Cloud. It needs `logs:write`, `metrics:write`, and `traces:write`. Store the header as a secret in the deployment platform; do not add it to an appsettings file or commit it.

The endpoint and protocol have non-secret defaults in `apps/api/MarketViewer.Api/appsettings.json`. The missing authorization header is the expected cause of `401 Unauthorized` export failures.

## Backtest Lambdas

The Terraform app stack can deploy `lambda-promtail` and subscribe the backtest orchestrator and worker CloudWatch log groups.

1. In Grafana Cloud, open **Observability > Cloud provider > AWS > Configuration > Logs with Lambda**.
2. Copy the Loki write address and username, then create a token with `logs:write`.
3. Pass the values to Terraform without putting them in a checked-in `.tfvars` file:

```powershell
$env:TF_VAR_enable_grafana_cloud_logs = "true"
$env:TF_VAR_grafana_loki_write_address = "https://logs-prod-...grafana.net/loki/api/v1/push"
$env:TF_VAR_grafana_loki_username = "<logs-instance-id>"
$env:TF_VAR_grafana_loki_token = "<cloud-access-policy-token>"
terraform -chdir=infra/tf/app plan
terraform -chdir=infra/tf/app apply
```

Terraform state contains Lambda environment variables, including the token, so the remote state bucket must remain access-controlled and encrypted.

Every orchestrator log carries `BacktestId`, start/end dates, AWS request ID, and function name. Every worker log carries `BacktestId`, `BacktestDate`, AWS request ID, and function name. These are structured fields, not Loki labels, to avoid high-cardinality label growth.

AWS Lambda advanced logging emits a top-level `level` field for Grafana severity detection. Lambda Promtail maps each CloudWatch log group to a `service_name` label, so the worker and orchestrator appear as separate services.

Example Grafana Explore queries:

```logql
{__aws_cloudwatch_log_group="/aws/lambda/stockmountain-dev-backtest-worker"} |= "\"BacktestDate\":\"2026-07-16\""
```

```logql
{__aws_cloudwatch_log_group=~"/aws/lambda/stockmountain-dev-backtest-.*"} |= "\"BacktestId\":\"<backtest-id>\""
```

For API logs:

```logql
{service_name="MarketViewer.Api"}
```

After deployment, trigger one API request and a one-day backtest, then verify both streams in Grafana Explore before enabling additional Lambda log groups.
