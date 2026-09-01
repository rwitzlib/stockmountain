# Runbook

Symptom-indexed. Start from what you're looking at (an error string, a stuck record, a
Grafana panel), not from a component. Each entry: mechanism → where to look → what to do.
Queries and the wide-event field reference live in [observability.md](observability.md);
retry/error semantics in [registry.md](registry.md#error-propagation--retry-budgets).
When you diagnose a failure mode that isn't listed here, **add it in the fixing PR** —
that is how this file accretes value.

## Worker logs: `Unhandled exception … HttpRequestException: Error while copying content to a stream … Broken pipe`

**Mechanism.** The handler never leaks exceptions and every outbound client call is
caught locally, so an *unhandled* HTTP write failure is the Lambda runtime itself POSTing
the function's return value to the runtime API — the payload exceeded the 6MB synchronous
response cap and the runtime API cut the connection mid-write. The process dies
(`platform.runtimeDone` ERROR, then a fresh `platform.start` = cold start), and the
orchestrator burns its 3-attempt day budget on a deterministic failure.

**Status.** Fixed 2026-09 by the S3 result handoff (ADR 0005): workers return a pointer,
so response size no longer scales with signal count. If this signature reappears, some
*other* return path grew: check `result_bytes` on the worker wide event (emitted in the
`finally`, so it survives the crash) and whatever the handler now returns inline.

**Worked diagnosis (2026-09-01, the incident that produced ADR 0005).** Grafana showed the
broken-pipe stack + `platform.report` ERRORs. Elimination: `MassiveClient` never throws;
`ScannerService` S3 calls individually caught; `FunctionHandler` try/caught everything ⇒
only the runtime's response POST writes HTTP unguarded. Payload math: ~600–800 bytes per
result × ~8–10k signals on a loose-filter day > 6MB. Confirmed by matching crashed
`requestId`s to their wide events' large `result_count`.

## Backtest stuck `Pending`

The API→orchestrator invoke is fire-and-forget with a discarded task
(`BacktestHandler.Create`); if the invoke is lost, nothing ever transitions the record.
Check the orchestrator log group for the backtest id — if there is no orchestrator wide
event at all, the invoke never landed. The record is safe to re-run (orchestrator refuses
non-`Pending` records). Durable fix is the planned SQS migration
([system.md § wrinkles](system.md#known-wrinkles-true-today-candidates-for-fixing)).

## Backtest stuck `InProgress`

The orchestrator started and died without its catch-all running (OOM, 15-min timeout) —
its own `platform.report` will say which. Long ranges × slow days can approach the 900s
ceiling ([registry § limits](registry.md#limits-and-budgets)). The record must be reset to
retry; check `fan_out_ms`/`day_span` on prior successful runs to judge whether the range
was simply too large.

## Record error: "Day could not be backtested after 3 attempts: …"

The suffix is the last attempt's failure. Find the three worker wide events for that
`backtest_date`:
- `error_type=InvalidOperationException` + "Market data setup failed" → the bulk market
  data object for that date is missing/unreadable (Flow 2 didn't produce it) or a warm
  container was memory-pressured. Check the market-data catalog for the date.
- "worker crashed (Unhandled)" → runtime death; read the crash lines just before the wide
  event; see broken-pipe entry above.
- "Failed to store worker results"/fetch errors → S3 throttling or IAM drift on the
  backtest-data bucket.

## Dropped signals: "TICKER at HH:mm: candle data unavailable (Status)"

Per-signal Massive failures after 3 in-worker attempts. A handful on a busy day is normal
(rate limiting); wholesale drops mean quota/auth trouble — check `dropped_signal_count`
across workers and the Massive token. Tuning knob: `MASSIVE_BATCH_SIZE` (worker
concurrency) — lower it if 429s dominate.

## Worker out-of-memory

`platform.report` `maxMemoryUsedMB` ≈ 3008. The DataCache holds ~185MB per loaded date and
compacts fully each invocation; OOM means one *single* day's working set outgrew the box
(many timeframes × loose filters). Options: raise `memory_size` in `lambda.tf` (credits
scale with GB-s — pricing implications), or narrow the filter set.

## Backtest results look wrong for a filter you just changed

If filter *semantics* changed without bumping `ScannerService.CacheVersion`, cached
`strategyEntries/` from the old semantics are silently reused per filter/date. Bump the
version (registry § storage), and re-bless golden tests. This rule predates this file —
it's enforced by the PR template's filter checklist.

## Local/sandbox build or test won't start

See the bootstrap appendix in [/AGENTS.md](../AGENTS.md) — pinned SDK fallback, the
global.json workaround, and the known-broken solution build are documented there.
