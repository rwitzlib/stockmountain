# ADR 0005: Worker Result S3 Handoff

## Status

Accepted (implemented 2026-09-01, PR #33)

## Context

The backtest worker returned its full `WorkerResponse` — every priced signal for the day —
in the synchronous Lambda invocation response. A signal-heavy day (~8–10k results at
600–800 serialized bytes each) exceeds Lambda's 6MB response cap, and an oversized return
does not fail cleanly: the .NET runtime dies with an unhandled
`HttpRequestException`/broken-pipe while POSTing the response to the runtime API. The
crash killed the warm container (cold start on the next invoke) and, because the failure
is deterministic, the orchestrator's 3-attempt retry tripled the damage. Grafana showed
recurring `platform.report` ERRORs with this signature (worked diagnosis:
`docs/runbook.md`).

## Decision

Workers always hand their full `WorkerResponse` to the orchestrator through S3 and return
only a small pointer:

- `WorkerResultStore` writes gzip+base64 JSON to
  `workerResults/{backtestId}/{yyyy-MM-dd}` in the backtest-data bucket; retried days
  overwrite the same key. The orchestrator resolves the pointer and folds fetch failures
  into the existing 3-attempt day budget.
- The handoff is **unconditional**, not size-gated. A size gate needs a serialize-to-
  measure pass, a guessed safety margin, and — decisively — makes the S3 branch the
  least-exercised path in production, which is exactly the dynamic that produced this
  bug. Per-day S3 cost/latency is negligible against minutes of compute.
- `RunDay` exceptions return a retryable `WorkerResultLocation.Error` instead of a stored
  failed-day response, so transient failures (market-data setup on a memory-pressured
  warm container) get fresh-container retries. A day failing all attempts still surfaces
  on the record via the placeholder path.

## Consequences

- Response size no longer scales with signal count; the crash class is closed. The new
  `result_bytes` wide-event field tracks stored payload size.
- The orchestrator gains a hard S3 read dependency for results (S3 was already
  load-bearing for the filter cache and market data).
- Both lambdas ship in one image, but a run in flight across a deploy straddles two
  contract versions and fails its days visibly (record `Failed`, re-runnable). Deploy at
  quiet moments.
- `workerResults/` accretes one small object per (backtest, day) with no expiration —
  deliberately deferred until S3 cost matters; the `shares/` lifecycle rule in `s3.tf`
  is the pattern to copy.
