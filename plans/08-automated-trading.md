# Plan 8: Automated trading strategies (optimus revival)

## Goal

Turn the half-built optimus paper-trading pipeline into a working automated trader:
a user backtests a strategy, creates a live strategy from that same configuration, and the
system executes it — first against the internal paper engine, then against Alpaca paper,
then Alpaca live with real money.

**v1 scope is a single user (Rob's personal account).** No OAuth, no multi-tenant broker
tokens, no compliance work. But every architectural choice below was made so that
"customers connect their own Alpaca account via OAuth" is an extension, not a rewrite.

## Decisions already made (do not re-litigate)

These came out of a design review on 2026-07-20:

1. **Broker: Alpaca.** Personal API keys in secrets for v1 (no OAuth needed for one's own
   account). Alpaca scales later to either customer model: OAuth for connect-your-own-account,
   or Broker API for platform-held accounts. Schwab is dead (7-day refresh token makes
   unattended bots impractical); `packages/schwab-api` and `SchwabAdapter` stay untouched but
   are not the path.
2. **Three execution tiers, each with a distinct job:**
   - **Internal paper** (`DefaultAdapter`) — answers "does the strategy work." Deterministic,
     fills at last-minute-close, matches backtest semantics, multi-tenant, no broker account
     needed. This stays; it is the future product funnel.
   - **Alpaca paper** — answers "does the integration work." Same adapter code as live,
     exercised against Alpaca's paper environment. Pre-live dress rehearsal.
   - **Alpaca live** — real money.
3. **Market orders only** in v1, whole shares. But even market orders get a minimal
   lifecycle: submit → poll status → confirm fill → record actual fill price/shares, with a
   timeout → cancel + alert path (a market order on a halted ticker sits unfilled forever,
   and scanners surface halted tickers precisely during volatility events). Full
   pending/partial-fill modeling and limit orders are deferred.
4. **Exits stay in our code, not at the broker** — no bracket/OCO orders implementing the
   logical stop-loss/take-profit (user explicitly rejected; wants levels private and exit
   logic unconstrained). **But** every live position gets a **disaster backstop**: a GTC
   stop-market order at ~3× the logical stop distance. Stop-market orders are held
   broker-side and never rest on a public book, so nothing leaks; the backstop exists only to
   bound loss when the bot is dead. It must be canceled before (or atomically with) any
   normal sell.
5. **Recovery tier (b):** startup reconciliation against Alpaca's positions/orders API +
   an independent heartbeat watchdog that **pages** on bot silence. No auto-flatten in v1
   (the backstop bounds tail risk; auto-flatten is the piece most likely to misfire).
6. **DynamoDB comes out of the signal hot path.** Today's design (scan result → DynamoDB
   write → DynamoDB Stream → EventBridge Pipes → SQS → consumer) uses persistence as
   transport and spends multi-second, uncontrollable tail latency before the consumer sees
   anything. New design: the scan producer publishes the signal **directly to SQS** and
   writes the DynamoDB scan-result record **asynchronously as an audit artifact**. Latency
   budget: order at broker within single-digit seconds of signal.
7. **Bots are not processes.** One evaluator holds the shared live bar cache and evaluates
   *all* active strategies per tick in a single pass; SQS consumers scale horizontally for
   execution. The commented-out per-user polling `TradingWorker` model is dead — delete it.
8. **Market clock/calendar comes from Alpaca's clock+calendar API** — replaces all
   hand-rolled DST offsets and hardcoded open/close times.
9. **PDT:** the $25k pattern-day-trader rule was replaced by real-time intraday margin
   requirements effective 2026-06-04 (FINRA Notice 26-10), but brokers may phase in until
   2027-10-20. Verify Alpaca's adoption status on the actual account before assuming
   unlimited intraday round trips (Phase 0).

Parked for later (attached to the "real customers" milestone, not to any code here):
legal/RIA analysis of who makes the trading decision, OAuth token management, SnapTrade-style
multi-broker aggregation, limit orders/partial fills, per-second bars and fill-quality
sensitivity, auto-flatten watchdog.

## Current state (context for a fresh reader)

The runner app is `apps/paper-bot-runner/Optimus` (deployed name "optimus"). What exists:

- **`Services/ScanQueueConsumer.cs`** — BackgroundService polling SQS; parses EventBridge
  Pipes DynamoDB-stream envelopes (`Models/DynamoDbStreamRecord.cs`), loads the scan result
  via `ScanResultsRepository`, calls `TradeExecutionService`.
- **`Services/TradeExecutionService.cs`** — the strongest existing code. Idempotent buy via
  `ExecutionDedupRepository.TryRecordExecution(strategyId, ticker, window)`, then an atomic
  reserve-execute-rollback against `StrategyStateRepository.TryReservePosition` (checks
  funds, max concurrent positions, ticker-not-open), cooldowns, and post-fill cost
  adjustment. Ports to Alpaca essentially unchanged.
- **`Services/SellWorker.cs`** — Quartz job; per open position makes an HTTP call to
  `api/stocks` for a price, evaluates timed exit + stop-loss/take-profit from
  `strategy.ExitSettings`, sells via adapter, updates state + balance history. Problems:
  hand-rolled DST offset, `MarketClose` hardcoded to 20:58, one HTTP price call per position
  per tick, and scan-based ("Other") exit conditions unimplemented.
- **`Services/TradingWorker.cs`** — 290 lines, 100% commented out, per-user 1-second polling
  loop with `"rob.witzlib@gmail.com"` hardcoded. Delete it; it misleads readers about the
  architecture.
- **`packages/optimus-adapter`** — `IAdapter` (`Buy(StrategyDto, ticker) → BuyResult`,
  `Sell(TradeRecord) → SellResult`, `GetPrice`), `AdapterFactory` keyed by
  `IntegrationType`, `DefaultAdapter` (internal paper: prices via `api/stocks`, writes
  `TradeRecord` with `TradeType.Paper`), `SchwabAdapter` (pure `NotImplementedException`).
- **`packages/optimus-infrastructure`** — Strategy/Trade/User/StrategyState/ExecutionDedup/
  ScanResults repositories (DynamoDB).
- **Contracts** — `StrategyDto` (`EntrySettings.Filters`, `ExitSettings`
  {StopLoss, TakeProfit, TimedExit}, `PositionSettings` {StartingBalance, Model,
  MaxConcurrentPositions, AllowSimultaneous, Cooldown}, `Integration`, `State`,
  `ComputeStrategyHash()`), `TradeRecord`, `StrategyStateRecord`, `BalanceHistoryRecord`.
- **`apps/api`** — `Jobs/SnapshotJob.cs` pulls full-market snapshots from Massive into the
  in-memory `BarCacheService` on a Quartz schedule (websocket keeps the latest minute
  fresh); `/api/scan` evaluates filters; `Controllers/Management/StrategyController.cs` has
  strategy CRUD but **no** backtest→strategy creation.

**Correction (found during Phase 1 implementation):** the producer *did* exist, just not
where first looked. `apps/api/MarketViewer.Api/Jobs/ScannerJob.cs` (every 15s) evaluates
each unique active strategy's entry filters via `ScanHandler` and writes scan records
through `IScanRepository` (marketviewer-infrastructure); `PopulateScannerJob` (every 5min)
feeds it from the `ACTIVE_STRATEGIES` DynamoDB partition, which `StrategyRepository`
maintains as a per-unique-hash refcount on strategy create/update/delete. Phase 1 was
therefore a rework of the producer's transport, not a from-scratch build.

## Phases

Ordering: 0 → 1 → 2 gets paper trading actually running end-to-end on the new hot path.
3 → 4 adds real money. 5 closes the product loop. Phase 3 can start in parallel with 1–2.

> **Status 2026-07-20:** Phase 0's PDT check is done (Alpaca has adopted the new
> intraday-margin framework, confirmed by Rob). Phases 1 and 2 are implemented — see the
> "Implemented" notes inside each phase for what shipped and where it deviated from the
> original sketch.

### Phase 0 — Accounts and verification spikes (no code)

- Create Alpaca account + paper account; obtain live and paper API key pairs; store in the
  same secrets mechanism the apps already use (see `.env` conventions in
  `paper-bot-runner/Optimus/Program.cs`).
- Confirm on the account whether Alpaca enforces legacy PDT or the new intraday-margin
  framework (decision 9). Determines which strategies are live-eligible if the account is
  under $25k.
- Confirm Alpaca API basics against paper: submit market order, poll order status, list
  positions, close position, clock/calendar endpoints, GTC stop-market support.

### Phase 1 — Signal producer + hot path rework

**New: `StrategyScanJob`** (Quartz job in `apps/api`, next to `SnapshotJob`) — the api app
is where the live bar cache already lives, so evaluation happens in-process there:

1. Each tick (start at 1/minute aligned to bar close; the interval is config so it can
   tighten toward per-second later): skip unless market open (Phase 2's calendar service);
   load all `StrategyStateType.Active` strategies (cache with short TTL).
2. Evaluate each strategy's `EntrySettings.Filters` against the in-memory cache in one pass
   — reuse the same scan internals `/api/scan` uses, not an HTTP self-call.
3. For each (strategy, ticker) hit: publish a **`SignalMessage`** directly to the existing
   SQS queue: `{ strategyId, ticker, window, signalAt }` where `window` is the bar-aligned
   unix timestamp already used by `ExecutionDedupRepository` for idempotency. Duplicate
   publishes are safe by design — dedup handles them.
4. Fire-and-forget the DynamoDB scan-result audit write (log failures; never block or fail
   the publish on it).

**Changed: `ScanQueueConsumer`** — parse `SignalMessage` instead of the EventBridge Pipes
envelope; drop `DynamoDbStreamRecord` parsing and the `ScanResultsRepository` read-back.
Keep both formats accepted during cutover if the Pipe is live anywhere; then remove.

**Infra (`infra/`):** remove the DynamoDB Stream → EventBridge Pipe wiring for scan
results; the SQS queue itself stays.

**Cleanup:** delete `Services/TradingWorker.cs`.

**Implemented 2026-07-20.** Deviations/specifics:

- Signal message is **hash-based with tickers inline** (`StrategySignalMessage
  { StrategyHash, Window, Tickers, SignalAtUnixMs }` in
  `packages/marketviewer-contracts/.../Messages/`), not per-(strategyId,ticker) — this
  preserves the existing hash→strategies fan-out via the GSI and the evaluation dedupe.
- Producer rework, not new job: `ScannerJob` now gates on market hours (2-min close
  buffer) and hands results to new `MarketViewer.Api/Services/SignalPublisher.cs`, which
  awaits the SQS publish (hot path) and fire-and-forgets the DynamoDB audit write.
- Consumer (`ScanQueueConsumer`) parses the new message first and logs signal-to-consume
  latency; the legacy Pipes-envelope path remains as a fallback until cutover, then gets
  deleted. The DDB read-back is gone from the new path.
- Infra: the queue/Pipe were never in Terraform (manual). Added
  `aws_sqs_queue.strategy_signals` (+DLQ, **300s retention by design** — stale signals
  must die, not replay) and `sqs:SendMessage` for the api user. Any manually created
  Pipe should be deleted at cutover. Optimus's AWS identity isn't TF-managed; grant it
  Receive/Delete on the new queue wherever its creds are managed.
- Config to set: api needs `SIGNAL_QUEUE_URL` (or `SignalQueue:QueueUrl`) and
  `SignalQueue:Enabled=true`; optimus `SqsConsumerConfig` points at the new queue URL.

### Phase 2 — Exit engine + market calendar

**New: `MarketCalendarService`** (in `packages/optimus-infrastructure` or a small shared
package): wraps Alpaca clock + calendar endpoints, cached daily; answers `IsOpen()`,
`NextClose()`, holidays/half-days. Replace every hand-rolled DST/open/close computation
(`SellWorker`, old worker remnants, `StrategyScanJob` gating).

**Rework `SellWorker`:**

- Price source: one batched price fetch per tick for the distinct tickers with open
  positions (snapshot endpoint or bar cache via api), not one HTTP call per position.
- Cadence: every few seconds during market hours (config), driven by the calendar service.
- Exit evaluation order per position: timed exit → stop-loss → take-profit (document the
  tie-break and keep it consistent with the backtester's same-bar semantics — see
  plan 01's note that same-bar ties go to the stop).
- Scan-based ("Other") exit conditions: evaluate exit filters the same way
  `StrategyScanJob` evaluates entry filters. If that drags in too much, ship v1 without it
  but leave the seam — `ShouldSell` already isolates the decision.
- On sell via a broker adapter: cancel the backstop order first (Phase 3 contract).

**Implemented 2026-07-20.** Deviations/specifics:

- `MarketCalendarService` lives in the new `packages/alpaca-client` package (started
  early; Phase 3 extends the same package with orders/positions). Calendar-based, cached
  daily, zero API calls per tick; fails open to standard weekday hours if Alpaca is
  unreachable so an outage can't silently stop trading. Registered in both api and
  optimus via `RegisterAlpacaClients` — both apps need `ALPACA_API_KEY_ID` /
  `ALPACA_API_SECRET_KEY` env vars (or `Alpaca:*` config; base URL defaults to paper).
- Exit decision logic extracted to pure `Optimus/Services/ExitEvaluator.cs`
  (timed → stop → take-profit, stop wins ties; halted/no-price tickers still honor the
  timed exit) with unit tests in `tests/optimus-unit-tests`.
- `SellWorker` now collects all open positions across strategies, fetches prices in
  batched `IMassiveClient.GetAllTickersSnapshot` calls (500 tickers/call) instead of one
  HTTP call per position, runs every 10 seconds (`[DisallowConcurrentExecution]`), and
  drops all hand-rolled DST/market-hours math. Scan-based (`ConditionalExit`) exits are
  still a TODO seam in `SellPositionIfApplicable`.

### Phase 3 — Alpaca adapter (can start in parallel with 1–2)

**New package: `packages/alpaca-client`** (mirror the layout of `packages/schwab-api`):
typed REST client for trading API v2 — orders (submit/get/cancel), positions
(list/get/close), account, clock, calendar. Paper vs live = base URL + key pair from
config. Straight `HttpClient` wrapper; no third-party SDK.

**Contracts:** extend `IntegrationType` with `AlpacaPaper` and `AlpacaLive` (two entries on
purpose — a strategy's tier must be explicit and auditable, and promotion is a visible
field change). v1 shortcut: `AdapterFactory` resolves both from app-config API keys,
bypassing the `user.Tokens` machinery (that machinery is the future OAuth seam; don't
build it now, don't rip it out).

**New: `AlpacaAdapter : IAdapter`** in `packages/optimus-adapter`:

- `Buy`: submit whole-share market order (shares = `PositionSettings.Model.Size` /
  current price, same math as `DefaultAdapter`); poll order status until `filled`
  (timeout ~30s → cancel + alert + `BuyResult.Failed`); on fill, write `TradeRecord`
  (`TradeType.Real` for live tier) with **actual** fill price/qty so
  `TradeExecutionService`'s `AdjustPositionCost` reconciles the reservation; then place the
  **backstop**: GTC stop-market at 3× the logical stop distance (derive from
  `ExitSettings.StopLoss`; multiplier in config), store its order id on the trade record.
- `Sell`: cancel backstop (tolerate already-filled/not-found — if the backstop filled, the
  position is already flat: record the close from the backstop fill instead of selling),
  then market sell, poll to fill, return `SellResult` with actual close value.
- Alpaca rate limit (~200 req/min) is fine at personal scale; put a note in the client for
  the multi-tenant future.

**Backtest parity note:** `DefaultAdapter` keeps its current semantics on purpose
(decision 2) — do not "improve" its fill model to look like Alpaca's.

### Phase 4 — Recovery + watchdog (required before any live order)

**Startup reconciliation** (hosted service in optimus, runs before consumers start):

1. Pull open `TradeRecord`s + `StrategyStateRecord`s; pull Alpaca positions + open orders.
2. Diff. Position at Alpaca with no open TradeRecord (or vice versa), quantity mismatches,
   missing/orphaned backstop orders → **alert with specifics; do not auto-trade** in v1.
   Clean matches → resume. Missing backstops for known positions → re-place them (safe,
   idempotent, protective).
3. Runs on every boot, so deploys and crashes get the same path.

**Heartbeat + watchdog:**

- Optimus writes a heartbeat (timestamped DynamoDB item or CloudWatch metric) every ~30s
  while the market is open.
- Independent watchdog — an EventBridge-scheduled Lambda, deliberately **not** on the same
  host/service as optimus — checks freshness each minute during market hours; stale beyond
  ~3 min → page via SNS (SMS + email). No auto-flatten (decision 5).

### Phase 5 — Backtest → strategy creation (the product loop)

- **API:** endpoint to create a strategy from a backtest id (StrategyController or the
  backtest handler): map the backtest request's filters/exit settings/position settings
  onto `StrategyDto` (`EntrySettings`, `ExitSettings`, `PositionSettings`), chosen
  `IntegrationType` tier, initial `State = Inactive`. The mapping is mostly 1:1 by design —
  keep it a dumb copy; any divergence between backtest config and strategy config is a bug
  factory.
- **Web:** "Create strategy" action on `BacktestDetailPage` → tier picker (internal paper /
  Alpaca paper / Alpaca live) + name → strategy management view (activate/deactivate, open
  positions, balance history from `BalanceHistoryRecord`s, recent signals from the audit
  trail).
- **Promotion gating, v1:** soft. Live tier selectable only by admin/owner role
  (`UserRole` gate already exists in `TradeExecutionService`); UI copy nudges paper-first.
  Hard promotion criteria (N days on paper, drawdown limits) are a later, multi-user
  concern.

## Verification

- **Phase 1:** with market open, activate one internal-paper strategy with permissive
  filters; watch signal publish → SQS → consumer → `DefaultAdapter` buy end-to-end; measure
  signal-to-execution latency (target: seconds); confirm audit record lands async; confirm
  duplicate signals are dropped by dedup.
- **Phase 2:** positions exit on timed/stop/take-profit against live prices; no HTTP-per-
  position fan-out in logs; holiday/half-day handled by calendar (unit-test with canned
  calendar responses).
- **Phase 3:** against **Alpaca paper only**: buy → fill confirmed → backstop visible in
  Alpaca order list; sell → backstop canceled → position flat; kill optimus mid-position →
  backstop survives at the broker (this is the entire point — verify it explicitly).
- **Phase 4:** kill optimus → page arrives within ~3 min; restart with a live paper
  position → reconciliation resumes cleanly; manually create a mismatch (close a position
  in Alpaca's UI) → restart alerts and refuses to auto-trade that strategy.
- **Live money gate:** all of the above green on Alpaca paper for multiple consecutive
  market days, plus Phase 0's PDT/margin answer confirmed.
