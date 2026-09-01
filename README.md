# StockMountain

Author stock-trading strategies as filter expressions, backtest them against historical
minute data, scan live markets, and paper/auto-trade — with metered compute and Stripe
billing.

**Start here:** [AGENTS.md](AGENTS.md) is the orientation protocol (humans welcome too);
[docs/system.md](docs/system.md) is the system map.

## Structure

```
apps/
  web/                    React SPA (Vite) — Railway
  api/                    MarketViewer API — Railway
  backtester/             Backtest orchestrator + worker (one image, two Lambdas)
  market-data-aggregator/ Market data orchestrator + aggregator Lambdas
  billing/                Monthly credit-refill Lambda
  paper-bot-runner/       Optimus paper/auto trading — Railway

packages/                 Shared libraries (contracts, filters DSL, infrastructure,
                          massive-client, alpaca-client, optimus-*, schwab-api)
tests/                    Per-project unit test suites + Playwright e2e
infra/tf/app/             Terraform (Lambdas, DynamoDB, S3, SQS, IAM, Grafana)
docs/                     System map, registries, runbook, observability, ADRs, filter docs
plans/                    Self-contained implementation plans with lifecycle ledger
api-collections/          API collections
tools/                    One-off operational scripts
```

## Build & test

Requires the .NET SDK pinned in `global.json` and Node 20+ for `apps/web`.
Build **per project** — the solution-wide build is known-broken:

```bash
dotnet build apps/backtester/Backtest.Lambda/Backtest.Lambda.csproj
dotnet test tests/backtest-lambda-unit-tests/Backtest.Lambda.UnitTests/Backtest.Lambda.UnitTests.csproj
cd apps/web && npm install && npm run dev
```

Deploy: `.github/workflows/app-deploy.yml` builds all images and applies terraform on
pushes touching `apps/**`, `packages/**`, `tests/**`, or `infra/tf/app/**`.
