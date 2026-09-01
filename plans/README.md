# Plans

Plans are **intentions**: self-contained hand-off units written for a fresh session with
no other context. They describe a future; once implemented, the present belongs to
[docs/system.md](../docs/system.md) and the registries, and the rationale to an ADR.
Do not implement from a plan without checking its status below first — and when you finish
one, flip its row in the same PR.

## Lifecycle ledger

Statuses are **self-verifying**: each row names a probe artifact whose existence in the
tree signals implementation, so any session can re-check a row with one grep instead of
trusting a date. Last swept 2026-09-01.

| Plan | Status | Probe (exists ⇒ implemented) |
|---|---|---|
| 01 exit-reason | done | `BacktestExitReason` on `BacktestEntryResult` |
| 02 mfe-mae | done | `MaxRunup` on `BacktestEntryResult` |
| 04 skipped-signals | open | `SkippedSignals` (no hits) |
| 05 entry-snapshot | open | `EntrySnapshot` (no hits) |
| 06 day-winloss-intraday | open | `DayWins` (no hits) |
| 07 share-backtest | done | `BacktestShareHandler` |
| 08 automated-trading | in progress (phased) | `packages/alpaca-client` exists; later phases open |
| 09 canonical-logging | done | `MarketViewer.Infrastructure/Logging/WideEvent.cs` |
| 10 live-data-fidelity | unverified | probe when picked up |
| 11 strategy-dsl-gaps | backlog (partial by design) | per-item |
| 12 shared-create-forms | done | `apps/web/src/components/forms/strategy/EntrySettingsForm.tsx` |
| 13 filter-builder-ux | done | `apps/web/src/components/filters/FilterChips.tsx` |
| 14 golden-filter-tests | done | `tests/marketviewer-filters-unit-tests/…/Golden/` |
| 15 filter-function-process | done | `RegistryParityTests`, `docs/filters/` |
| 16 billing-and-e2e | done | `apps/billing`, `tests/e2e` |
| 17 embedded-checkout-annual | done | `BillingInterval` on `UserRecord` |
| 18 scanner | done | `apps/web/src/pages/ScannerEditorPage.tsx` |

Authoring a new plan: number it, keep it hand-off-complete (a fresh session with only that
file must be able to implement it), state its probe artifact up front, record settled
design arguments in a "Decisions already made" section (implementers don't re-litigate),
and add the row above.

# Backtest payload enrichment plans

Independent plans to enrich the backtest result payload, ranked by value. Each file is
self-contained (written to be handed to a fresh chat instance with no other context), but they
touch overlapping files, so implement them **one at a time** and rebase between them.

| # | Plan | Adds | Primary files touched |
|---|------|------|----------------------|
| 1 | [01-exit-reason.md](01-exit-reason.md) | `exitReason` per trade | WorkerFunction, contracts, simulator |
| 2 | [02-mfe-mae.md](02-mfe-mae.md) | `maxRunup`/`maxDrawdown` per trade | WorkerFunction, contracts, simulator |
| 4 | [04-skipped-signals.md](04-skipped-signals.md) | per-day signal/skip counts | BacktestPortfolioSimulator, equity point contract |
| 5 | [05-entry-snapshot.md](05-entry-snapshot.md) | filter values at entry per trade | ScannerService, MarketViewer.Filters, contracts |
| 6 | [06-day-winloss-intraday.md](06-day-winloss-intraday.md) | `dayWins`/`dayLosses` + intraday balance extremes | BacktestPortfolioSimulator, equity point contract |

Conflict clusters (expect merge friction if run in parallel):

- **1 + 2** both extend `BacktestEntryResult`/`BacktestExecutedTrade` and the same block of
  `WorkerFunction.GetBacktestResult` — do 1 first, then 2.
- **4 + 6** both extend `BacktestEquityPoint` and `BacktestPortfolioSimulator.SimulateStrategy` —
  do in either order, sequentially.
- 5 is mostly isolated from the others.

All plans share the same backward-compatibility rule: results already persisted to S3
(`backtestResults/{userId}/{id}/portfolio.json`) will not have the new fields, so every new
field is optional end-to-end and the web UI must render sensibly when it is absent.

## Feature plans

| # | Plan | Adds | Primary files touched |
|---|------|------|----------------------|
| 7 | [07-share-backtest.md](07-share-backtest.md) | public share links for backtests | BacktestController, new ShareController, BacktestRepository, contracts, s3.tf, share dialog + public SPA route |
| 8 | [08-automated-trading.md](08-automated-trading.md) | automated trading via optimus: signal producer, exit engine, Alpaca adapter, watchdog, backtest→strategy flow | paper-bot-runner (optimus), optimus-adapter, optimus-infrastructure, new alpaca-client, apps/api jobs, infra |
| 9 | [09-canonical-logging.md](09-canonical-logging.md) | wide-event (canonical) logging in backtest lambdas + live Lambda cost dashboards/alerts in Grafana | Backtest.Lambda handlers, MarketViewer.Infrastructure, WorkerRequest contract, Dockerfiles, app-deploy.yml, observability.tf, new Grafana terraform |
| 10 | [10-live-data-fidelity.md](10-live-data-fidelity.md) | live data-path reliability: websocket reconnect, completed-bar ring buffer + rollover diff wide event, snapshot probe at :00 + gap backfill | StocksLiveFeed, MemoryMarketCache/IMarketCache, SnapshotJob, BarCacheService, ScanHandler, CacheWarmupService |
| 11 | [11-strategy-dsl-gaps.md](11-strategy-dsl-gaps.md) | backlog of strategy-enabling capabilities: conditional exits, rvol, arithmetic in DSL, gap primitives, trailing stops, ATR, shorts, regime filters + known filter bugs | MarketViewer.Filters parser/functions, StrategyExitSettings, WorkerFunction, Optimus |
| 12 | [12-shared-create-forms.md](12-shared-create-forms.md) | rebuild BacktestCreatePage on the shared strategy form components, delete legacy PascalCase request types (fixes `'value'`/`'PercentOfEquity'` enum bugs + cooldown-off 400); contract change: cooldown optional, stopLoss/takeProfit/timedExit mandatory; dead-code cleanup | StrategyPositionSettings/StrategyExitSettings contracts, Backtest/Strategy handlers, apps/web (BacktestCreatePage, BacktestDetailPage, types, forms/strategy/*) |
| 13 | [13-filter-builder-ux.md](13-filter-builder-ux.md) | filter authoring UX: `POST /filters/validate` backed by the real parser, smart text input (autocomplete, signature hints, English echo), AST-driven clickable chips, recents/templates library | new FiltersController + handler/contracts, FilterComposer rework, new FilterChips, EntrySettingsForm/FilterDisplay render swap |
| 14 | [14-golden-filter-tests.md](14-golden-filter-tests.md) | golden filter test suite: real Massive fixtures (DST/half-day/1d/1h), Python-computed reference indicator values, blessed filter-outcome snapshots, candle-rebuild/`MergePreviousPeriod`/`ScannerService` wiring tests; safety net for plan-11 fixes | tests/marketviewer-filters-unit-tests (Golden/, TestData/Golden/), tests/backtest-lambda-unit-tests, new tools/golden/, `EvaluateSeries` hook in IndicatorExpressionEngine |
| 15 | [15-filter-function-process.md](15-filter-function-process.md) | scalable process for adding/modifying filter functions: `[FilterFunction]` attribute as single source of truth (parser/catalog/heuristics/contexts derived by reflection), context enforcement in `/filters/validate`, parity tests, public `/docs/filters/:name` pages rendered from `docs/filters/*.md`, `.claude/skills/add-filter-function` skill + PR template, `/stocks` off `StudyType` | MarketViewer.Filters (new Registry/, ExpressionParser, heuristics), FilterValidateHandler + contracts, new docs/filters/, apps/web docs route + FilterComposer, .claude/skills/, tests (RegistryParityTests) |
| 16 | [16-billing-and-e2e.md](16-billing-and-e2e.md) | Stripe billing: subscription checkout, hybrid credit refill (webhook + monthly lambda), PurchasedCredits top-up packs, billing ledger/LTV, Customer Portal, /billing page, UserRole rename Free/Pro/Premium + Playwright e2e suite (billing + backtest) | new BillingController/StripeWebhookController, UserRecord/UserRepository, Backtest.Lambda orchestrator, new Billing.Lambda, dynamodb.tf/eventbridge.tf/lambda.tf, apps/web (/billing, LandingPage), new tests/e2e |
| 17 | [17-embedded-checkout-and-annual.md](17-embedded-checkout-and-annual.md) | Stripe Embedded Checkout modal (purchases stay on-site, promo codes) + annual billing at 20% off ($279/$949) with 1-month bonus credits, refill-lambda expansion to annuals, Portal monthly↔annual switching | StripeGateway/BillingController/contracts, StripeWebhookProcessor, UserRecord (BillingInterval), Billing.Lambda, apps/web (BillingPage modal + toggle, LandingPage), tests/e2e billing.spec |
| 18 | [18-scanner.md](18-scanner.md) | saved user scanners (CRUD) on the shared filter/create stack: Pro-gated `/scanner` endpoints + ScannerRepository (strategy-store reuse), scanner list + editor/run pages composing `EntrySettingsForm`/`FilterComposer`/`FilterChips`, on-demand runs via `POST /scan`, server-side expression validation on scanner/strategy/backtest create, scanner↔backtest/strategy prefill handoffs, legacy `ScanArgument` UI deleted | new ScannerController + handlers/validators/repository, ScanController (rename + Pro tier), FilterValidateHandler (extract core), StrategyRepository, apps/web (ScannerPage rewrite, new ScannerEditorPage, scannerApi, EntrySettingsForm context prop, legacy form deletions) |

Plan 7 is independent of the enrichment plans. Plan 8 is a multi-phase roadmap (phases 0–5),
not a single hand-off unit — implement it phase by phase; its "Decisions already made"
section records design-review outcomes and should not be re-litigated by implementers.
