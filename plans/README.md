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

Plan 7 is independent of the enrichment plans. Plan 8 is a multi-phase roadmap (phases 0–5),
not a single hand-off unit — implement it phase by phase; its "Decisions already made"
section records design-review outcomes and should not be re-litigated by implementers.
