# Plan 10: Live market-data fidelity (websocket hardening, rollover diff, snapshot timing)

> **Status: implemented 2026-07-26** (all four workstreams + unit tests). Remaining manual
> verification: kill the websocket mid-session in dev and watch reconnect/staleness logs.

## Goal

Live paper-trading results diverge from backtests over the same days. The backtester evaluates
only completed, canonical minute aggregates at exact minute boundaries; the live scanner
(`ScannerJob`, every 15s) evaluates at `DateTimeOffset.Now` against intraday history assembled
from snapshot polls plus an in-progress bar built from websocket per-second aggregates. Some
divergence is inherent (partial-bar entries are a design choice), but three pieces of the live
data path are silently unreliable and make the divergence unmeasurable:

1. **The websocket feed cannot recover from any disconnect.** One exception or server-side
   close kills `StocksLiveFeed` for the rest of the day; every live bar silently freezes.
2. **Completed websocket bars are discarded unverified.** We assume the snapshot bar that
   replaces them is equivalent, but never measure it, and there is a :00–:03 window each minute
   where the just-closed bar exists nowhere.
3. **The snapshot poll runs at second :03 as a guess** at when Massive has finalized the n−1
   bar, and a missed or stale poll leaves a permanent hole in the day's minute history (the
   cache is append-only), skewing every lookback indicator for the rest of the session.

This plan fixes all three. It deliberately does **not** change entry semantics (live still
enters intra-minute on partial bars) — it makes the live data trustworthy and the
live-vs-snapshot difference observable, so a later decision about entry semantics can be made
from data.

## Architecture context (for a fresh reader)

All in `apps/api/MarketViewer.Api` + `packages/marketviewer-infrastructure` +
`packages/marketviewer-contracts` unless noted:

- `MarketViewer.Infrastructure/Services/StocksLiveFeed.cs` — `BackgroundService` holding one
  websocket to Massive (`A.*` per-second aggregates). Each message → `IMarketCache.AddLiveBar`.
- `MarketViewer.Contracts/Caching/MemoryMarketCache.cs` — `AddLiveBar` folds second aggregates
  into a single in-progress `Bar` per ticker (cache key = raw ticker string). On minute
  rollover the completed bar is **overwritten** (only SPY keeps a `SPY_LIVE` list).
  `LiveMarketCache` (same folder) subclasses it for the live API; `DictionaryMarketCache`
  throws `NotImplementedException` for live-bar methods (backtester never uses them).
- `MarketViewer.Api/Jobs/SnapshotJob.cs` — Quartz cron `3 * 4-19 ? * MON-FRI` (second :03 of
  every minute, ET). Calls `IMassiveClient.GetAllTickersSnapshot(null)`; each ticker's
  `snapshot.Minute` is the **last completed** minute bar. Applies via
  `BarCacheService.AddBarToCache`, which for minute bars appends only when
  `newCandle.Timestamp > lastCandle.Timestamp` — append-only, no backfill.
- `MarketViewer.Api/Jobs/ScannerJob.cs` — cron `0/15 * 4-19`. Runs `ScanHandler`
  (`packages/marketviewer-application/.../Market/Scan/ScanHandler.cs`), which clones the cached
  `StocksResponse` and appends `GetLiveBar(ticker)` via `TryAddBarToResponse`.
- Schedules live in `MarketViewer.Api/HostedServices/CacheWarmupService.cs`.
- `packages/massive-client/Massive.Client/MassiveClient.cs` — `GetAllTickersSnapshot(string tickers, ...)`
  already accepts a comma-separated ticker filter, so a cheap single-ticker probe needs no new
  client method.

**Data-plan constraint (affects everything below):** the Massive subscription is currently the
**15-minute-delayed** plan. Websocket aggregates and snapshot bars both arrive on the same
delayed clock, so bar *content* matches the canonical aggregates the backtester uses — only
arrival time shifts. Two wall-clock touchpoints break under this and are fixed in Workstream D;
the freshness probe in C1 is specified delay-agnostically so nothing changes when the plan is
upgraded to real-time. Note also: the current paper adapter (`DefaultAdapter` in
`packages/optimus-adapter`) fills trades from the delayed snapshot's last minute close, so
signals and fills share the delayed clock — paper trading is internally consistent, just
running 15 minutes behind the tape. The plan-08 Alpaca integration breaks that (real-time
fills against 15-minute-stale signals); **upgrade to the real-time data plan before wiring
Alpaca execution.**

Implement the workstreams in order — B depends on nothing but is verified by C's logging, and
C's gap backfill uses B's ring buffer.

---

## Workstream A: `StocksLiveFeed` reconnect hardening

File: `MarketViewer.Infrastructure/Services/StocksLiveFeed.cs`

1. **Reconnect loop.** Move socket creation inside the `while (!cancellationToken.IsCancellationRequested)`
   loop (`new ClientWebSocket()` per attempt — a `ClientWebSocket` cannot be reused after
   close/abort) and move the `try/catch` inside it too, so an exception logs, disposes the
   socket, waits, and retries instead of ending `ExecuteAsync`. Same for the server-initiated
   `Close` frame (currently `return` at line ~63) — treat it as "reconnect", not "stop".
2. **Reset the handshake flags** (`_isConnected`, `_isAuthenticated`, `_isSubscribed`) at the
   top of each connection attempt, not only in `finally` at method end.
3. **Backoff:** exponential from 1s doubling to a 30s cap; reset to 1s after a successful
   subscribe. Honor the cancellation token in the delay.
4. **Staleness watchdog.** A dead TCP peer can leave `ReceiveAsync` hanging forever with no
   exception. Wrap it with a timeout (linked `CancellationTokenSource`, ~60s): on timeout
   during a period when messages are expected, log and force a reconnect. Keep it simple —
   a receive timeout is a reconnect, period; no market-hours awareness needed (Massive sends
   status/keepalive traffic, and reconnecting overnight is harmless).
5. **Remove the oversized-message drop** (`if (result.Count >= MaxBufferSize) { ... continue; }`).
   The `StringBuilder` accumulation above it already handles messages larger than the 64KB
   buffer via `EndOfMessage`; this check can only fire when a final chunk exactly fills the
   buffer, in which case it silently discards real aggregates. Delete it. (Optional guard:
   if `messageBuilder.Length` exceeds a sane cap like 8MB, log and clear — but never drop
   silently.)
6. **Connection-lifecycle logging** (plan 09 spirit): one structured log line per state change
   — connected, subscribed, disconnected (with reason: exception type / close frame / stale),
   reconnect attempt number, seconds since last message. These are rare events; log them all.

No interface changes. `Program.cs` registration is untouched.

## Workstream B: completed-bar ring buffer + rollover diff + blind-window fix

### B1. Keep recent completed websocket bars

Files: `MarketViewer.Contracts/Caching/IMarketCache.cs`, `MemoryMarketCache.cs`,
`DictionaryMarketCache.cs`

- In `MemoryMarketCache.AddLiveBar`, on minute rollover (the branch that currently overwrites
  the cached bar), push the outgoing completed bar into a small per-ticker ring buffer before
  replacing: cache key `LiveBars/{ticker}` holding a bounded `List<Bar>` (or fixed-size
  array + index) of the last **5** completed websocket bars, newest last. Set the same
  16h expiration used elsewhere. This replaces the SPY-only `SPY_LIVE` special case — keep
  `SPY_LIVE` writes for now (the tools endpoint reads it) but note it as removable.
- Add to `IMarketCache`:
  ```csharp
  /// Last few websocket-completed minute bars, oldest→newest. Empty if feed is down.
  IReadOnlyList<Bar> GetRecentLiveBars(string ticker);
  ```
  `DictionaryMarketCache` returns `[]` (do not throw — ScanHandler will call this on every
  scan, and the backtester shares that code path in tests).
- Thread-safety note: `AddLiveBar` is called from the single websocket receive loop, but
  readers (scans, snapshot job) run concurrently. Return a copy or use an immutable list
  swap; do not hand callers the mutable ring.

### B2. Rollover diff wide event (websocket bar vs snapshot bar)

File: `MarketViewer.Api/Jobs/SnapshotJob.cs`

After applying each snapshot minute bar, look up `GetRecentLiveBars(ticker)` for a bar with the
same timestamp and compare Close / High / Low / Volume / Vwap. Accumulate per-run counters —
do **not** log per ticker:

- `wsMatched` (all fields within tolerance: price fields exact-ish `< $0.005`, volume within
  0.5%), `wsPriceMismatch`, `wsVolumeMismatch`, `wsMissing` (snapshot bar has no websocket
  counterpart — the feed was down or the ticker isn't streaming), `snapshotOnlyTickers` total.

Emit **one** wide event per run (structured `logger.LogInformation` with named fields, per the
plan-09 convention) with those counts plus the top ~10 worst offenders by relative volume
delta (`ticker`, both bars' OHLCV). A sustained spike in `wsMissing` doubles as the feed
liveness alarm; the match-rate distribution is the empirical answer to "could we trust
websocket bars without snapshot replacement?".

### B3. Close the :00–:03 blind window in scans

File: `packages/marketviewer-application/.../Market/Scan/ScanHandler.cs` (`TryAddBarToResponse`)

Between minute close and the snapshot poll landing, the just-closed bar M exists only in the
ring buffer (the live bar has already rolled to M+1). In `ScanTicker`, before calling
`TryAddBarToResponse`, fetch `GetRecentLiveBars(ticker)` and pass it in; inside the minute
branch, first append any ring-buffer bars whose timestamp is **greater than the last history
bar's and less than the live bar's minute** (normally zero or one bar; the timestamp guard
makes it idempotent once the snapshot bar lands). Only for `1 minute` timeframe — the hour
branch already merges the live bar and the missing 0–3s of an hour bar is noise.

## Workstream C: snapshot at :00 with freshness probe + gap backfill

Files: `MarketViewer.Api/Jobs/SnapshotJob.cs`, `MarketViewer.Api/Services/BarCacheService.cs`,
`MarketViewer.Api/HostedServices/CacheWarmupService.cs`, config in `appsettings*.json`

### C1. Probe-then-fetch

- Change the snapshot trigger cron from `3 * 4-19 ? * MON-FRI` to `0 * 4-19 ? * MON-FRI` and
  add `[DisallowConcurrentExecution]` to `SnapshotJob` so a slow probe loop can never overlap
  the next minute's run.
- At the top of `Execute`: probe with `GetAllTickersSnapshot("SPY")` — cheap single-ticker
  response. Freshness must be judged by **advancement, not wall clock**: the account is on
  Massive's 15-minute-delayed plan, so "SPY's bar == wall-clock n−1" would never be true and
  the probe would exhaust its retries every single minute. Instead, keep the SPY minute
  timestamp applied by the previous run (a field on the job's supporting state or a cache
  entry, seeded on warmup); the snapshot is fresh when the probed SPY timestamp is **greater
  than the last applied one**. This works identically on the delayed and real-time plans.
  If not yet advanced, delay ~400ms and retry, up to 5 attempts. On exhaustion, fetch the
  full snapshot anyway (a partially-stale snapshot mostly no-ops through the append-only
  guard; C2 catches whatever it misses next minute). Also log `dataLagSeconds` =
  wall now − probed SPY timestamp in the wide event — it should sit near 900s on the delayed
  plan and near 0–5s after a plan upgrade, and a drift is itself an alarm.
- SPY is the sentinel because per-ticker freshness is undecidable — an illiquid ticker showing
  an old bar may simply not have traded. SPY prints every minute of extended hours.
- Add to the wide event from B2: `probeAttempts`, `probeLatencyMs`, `probeExhausted`, and the
  accepted snapshot's SPY timestamp. Config knobs (`Snapshot:ProbeMaxAttempts`,
  `Snapshot:ProbeDelayMs`) in `appsettings*.json` next to the existing `ScanConfig` section.

### C2. Gap detection + backfill

- `BarCacheService.AddBarToCache`, minute branch: when appending, detect a gap —
  `newCandle.Timestamp - lastCandle.Timestamp > 60_000`. Don't fix it inline (this runs inside
  a tight per-ticker loop); return gap info to the caller. Suggested shape: give the minute
  branch an out-param or make the method return a small result struct
  `(Bar Added, long? GapFromTs, long? GapToTs)` — pick whichever reads cleaner at the two call
  sites (`SnapshotJob`, `CacheWarmupService`).
- `SnapshotJob` collects gap tickers during the apply loop, then backfills in two tiers:
  1. **Ring buffer first** (free): fill missing minutes from `GetRecentLiveBars(ticker)` —
     covers the common case where one snapshot poll failed but the websocket was alive.
     Insert in timestamp order; the `StocksResponse.Results` list must stay sorted.
  2. **REST fallback, bounded:** for tickers still missing minutes, fetch that day's minute
     aggregates from `IMassiveClient` (existing aggregates method used by the warmup path) and
     splice the missing bars in. Cap at ~50 tickers per run; beyond that (e.g. several
     consecutive failed polls), log the deficit in the wide event and let the next run continue
     — do not stampede the REST API.
- Note for reviewers: a gap in an illiquid ticker can be legitimate (no trades that minute).
  Tier-1/tier-2 both naturally handle this — the sources simply have no bar for that minute —
  so no special-casing; just don't count "gap detected, sources had nothing" as an error in
  the wide event (`gapsFilled` vs `gapsEmpty` counters).

## Workstream D: scan on the data clock, not the wall clock

Files: `MarketViewer.Api/Jobs/ScannerJob.cs`, `MarketViewer.Api/appsettings*.json`
(config key `MarketData:DelayMinutes`, `15` today, `0` after a plan upgrade)

Two places assume wall clock == data clock, which is off by 15 minutes on the delayed plan:

1. **`evaluationTime` drives the `time` filter field.** `ScanHandler` defaults to
   `DateTimeOffset.Now` (ScanHandler.cs:72), and `DataAccessExpression.EvaluateTime` documents
   that "time" is the evaluation clock, deliberately not the last bar's timestamp (so stale
   thin tickers can't keep passing a `time < 10:00` gate). On the delayed plan that clock is
   15 minutes ahead of the data: a `time < 630` filter live stops matching when the *data* is
   only at 10:15, and a `time > X` gate opens 15 data-minutes late. Every time-of-day-gated
   strategy diverges from backtest by exactly the delay.

   Fix in `ScannerJob` (not ScanHandler — keep the handler's default for ad-hoc API scans):
   set `scanRequest.Timestamp = DateTimeOffset.Now - TimeSpan.FromMinutes(config.DelayMinutes)`.
   That single assignment flows into both `evaluationTime` and the cache lookups
   (`LiveMarketCache` overrides `DateKey`, so the date segment stays correct). Do **not** use
   a per-ticker bar timestamp as the clock — that regresses the stale-thin-ticker guard the
   doc comment describes; a single global shifted clock preserves it.

2. **The market-hours gate cuts off the end of the session in data-time.**
   `ScannerJob` returns early when `!IsMarketOpen(CloseBuffer)` — i.e. after 15:58 wall. But
   at 15:58 wall the newest data is 15:43, so bars from data-time 15:43–16:00 are **never
   scanned live**, while the backtest scans through 16:00. Evaluate the gate at the shifted
   timestamp too (`IsMarketOpen` relative to `now − DelayMinutes`, keeping `CloseBuffer`), so
   scans continue until ~16:13 wall and cover the same data-time range as the backtest. This
   is correct for the current paper adapter, whose fills come from the same delayed snapshot;
   revisit when real (Alpaca) execution lands, because signals for the last `DelayMinutes` of
   the session are structurally untradeable in the real market — another reason the plan-08
   Alpaca phase requires the real-time data plan first.

Also add to the B2/C1 wide event: `scanDataTime` (the shifted timestamp) alongside wall time,
so staleness is visible in every event, not just the snapshot probe's `dataLagSeconds`.

## Explicitly out of scope (follow-up plans)

- **Decision-time capture**: persisting the exact evaluation inputs (bars + live partial bar)
  to S3 when a scan signal fires, keyed `strategyHash/window/ticker` — the direct
  live-vs-backtest reconciliation tool discussed alongside this plan.
- **End-of-day dump** of the live minute cache to S3 in warmup-file format for canonical diffing.
- Any change to entry semantics (completed-bar-only entries) — decide after B2/C data lands.
- Removing the snapshot-replacement design in favor of websocket bars — same.
- **Minute-history depth parity** (noted 2026-08-17, plan 14 follow-up 3): live holds 5 sessions of
  1-minute bars (`MarketCacheWarmer.MinuteFileCount`); the backtester loads the scan date plus 1 prior
  session (`DataCache.PreviousMinuteSessions`, memory-bound in the 3GB worker). Warm-ups under ~960
  bars are equivalent; long EMA-style seeds differ slightly. Revisit if the worker's memory model
  changes (streaming deserialize / no clones).

## Tests

- **MemoryMarketCache** (`tests/marketviewer-application-unit-tests` has cache-adjacent tests;
  add a dedicated fixture if none exists): rollover pushes completed bar into ring buffer;
  buffer bounded at 5; `GetRecentLiveBars` ordering oldest→newest; concurrent read during
  `AddLiveBar` returns a stable copy.
- **ScanHandler** (`ScanHandlerUnitTests.cs` already seeds live bars via `AddLiveBar`): scan in
  the blind window — history ends at M−1, ring has M, live bar is M+1 → response contains
  M−1, M, M+1; after the snapshot appends M, re-scan does not duplicate M.
- **BarCacheService**: gap detection fires only for >60s deltas; contiguous appends report none.
- **SnapshotJob** (mock `IMassiveClient`): stale-SPY probe retries then accepts; fresh probe
  short-circuits on attempt 1; exhaustion still applies the full snapshot; gap backfill
  prefers ring buffer and only calls REST for still-missing minutes; diff counters classify
  matched/mismatched/missing websocket bars.
- **StocksLiveFeed**: the receive loop is not unit-testable as written and refactoring it for
  testability is not required by this plan. Verify manually in dev: kill the network mid-day
  (or force-close via a proxy), confirm reconnect logs and that live bars resume; confirm the
  staleness watchdog fires on a hung connection.
- `dotnet build stockmountain.sln` and the full existing test suites stay green.

## Acceptance criteria

- [ ] Killing the websocket connection mid-session results in automatic reconnect (with
      backoff) and live bars resuming, with lifecycle log lines for each state change.
- [ ] No message-size code path silently discards aggregates.
- [ ] Every snapshot run emits one wide event with websocket-vs-snapshot diff counters, probe
      stats, and gap-backfill counters.
- [ ] A scan issued between minute close and snapshot arrival sees the just-closed bar (from
      the ring buffer), and the bar is not duplicated after the snapshot lands.
- [ ] Snapshot job runs at :00 with the SPY probe; a stale first response is retried and the
      applied history bar for a liquid ticker is minute n−1, not n−2.
- [ ] A skipped snapshot poll no longer leaves a permanent hole: the next run backfills from
      the ring buffer (or REST within the cap) and reports it in the wide event.
- [ ] After a few live sessions, the wide events answer: match rate of websocket bars vs
      snapshot bars, frequency of feed outages, frequency of stale/late snapshots.
- [ ] On the delayed plan: the snapshot probe accepts on advancement (no perpetual retry
      exhaustion), `dataLagSeconds` reads ~900, a `time`-gated filter fires at the same
      *data* minute as a backtest of the same day, and scans keep running until
      ~16:13 wall so data-time coverage matches the backtest's 9:30–16:00.
- [ ] Setting `MarketData:DelayMinutes` to `0` restores today's wall-clock behavior unchanged
      (safe for a future real-time plan upgrade).
