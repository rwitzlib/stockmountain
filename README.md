# StockMountain Monorepo

StockMountain is consolidated into a single repository with deployable apps, shared packages, tests, and infrastructure.

## Structure

```
apps/
  web/                  React frontend (Vite)
  api/                  MarketViewer API (legacy backend)
  paper-bot-runner/     Optimus paper trading worker
  backtest-worker/      Backtest Lambda worker
  backtester-api/       Local backtest API wrapper
  management-api/       Deployment management API
  market-data-aggregator/ Market Data Aggregator Lambda
  kesha/                Kesha Lambda
  marketviewer-web/     Legacy Blazor UI (deprecated)

packages/
  marketviewer-*        Shared MarketViewer libraries
  optimus-*             Optimus adapter/infrastructure
  schwab-api/           Schwab broker integration

tests/                  Unit and integration tests
infra/                  Terraform, monitoring, local dev
api-collections/        Bruno API collections
```

## Prerequisites

- .NET 8 SDK
- Node.js 20+ (for `apps/web`)

## Build

```bash
dotnet build StockMountain.slnx
```

## Migration notes

- Source repos were snapshot-copied into this layout; old repos remain for git history.
- Cross-repo `MarketViewer.*` NuGet references were replaced with project references.
- `MarketDataProvider` was excluded as obsolete; market data aggregation now uses shared MarketViewer contract enums.
- `apps/marketviewer-web` (legacy Blazor UI) is present but excluded from the solution until updated.
- `Backtest.Lambda` was updated for renamed contract types (`BacktestContextRecord`, `BalanceChange`).

## Frontend

```bash
cd apps/web
npm install
npm run dev
```

## Notes

- Legacy MarketViewer code coexists with future V1 work; names are preserved during migration.
- MarketDataProvider was excluded as obsolete; Market Data Aggregator uses shared MarketViewer contract enums.
- Old separate repos remain read-only for historical git history.
