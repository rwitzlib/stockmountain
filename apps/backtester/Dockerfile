FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY apps/backtester/Backtest.Lambda/Backtest.Lambda.csproj apps/backtester/Backtest.Lambda/
COPY packages/marketviewer-contracts/MarketViewer.Contracts/MarketViewer.Contracts.csproj packages/marketviewer-contracts/MarketViewer.Contracts/
COPY packages/marketviewer-core/MarketViewer.Core/MarketViewer.Core.csproj packages/marketviewer-core/MarketViewer.Core/
COPY packages/marketviewer-filters/MarketViewer.Filters/MarketViewer.Filters.csproj packages/marketviewer-filters/MarketViewer.Filters/
COPY packages/marketviewer-infrastructure/MarketViewer.Infrastructure/MarketViewer.Infrastructure.csproj packages/marketviewer-infrastructure/MarketViewer.Infrastructure/
COPY packages/marketviewer-studies/MarketViewer.Studies/MarketViewer.Studies.csproj packages/marketviewer-studies/MarketViewer.Studies/

RUN dotnet restore apps/backtester/Backtest.Lambda/Backtest.Lambda.csproj

COPY apps/backtester/Backtest.Lambda/ apps/backtester/Backtest.Lambda/
COPY packages/marketviewer-contracts/MarketViewer.Contracts/ packages/marketviewer-contracts/MarketViewer.Contracts/
COPY packages/marketviewer-core/MarketViewer.Core/ packages/marketviewer-core/MarketViewer.Core/
COPY packages/marketviewer-filters/MarketViewer.Filters/ packages/marketviewer-filters/MarketViewer.Filters/
COPY packages/marketviewer-infrastructure/MarketViewer.Infrastructure/ packages/marketviewer-infrastructure/MarketViewer.Infrastructure/
COPY packages/marketviewer-studies/MarketViewer.Studies/ packages/marketviewer-studies/MarketViewer.Studies/

RUN dotnet publish apps/backtester/Backtest.Lambda/Backtest.Lambda.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o /app/publish \
    --no-restore

FROM public.ecr.aws/lambda/dotnet:10

COPY --from=build /app/publish ${LAMBDA_TASK_ROOT}

CMD ["Backtest.Lambda::Backtest.Lambda.WorkerFunction::FunctionHandler"]
