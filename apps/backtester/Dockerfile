FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY apps/backtester/Backtest.Lambda/Backtest.Lambda.csproj apps/backtester/Backtest.Lambda/
COPY packages/marketviewer-contracts/MarketViewer.Contracts/MarketViewer.Contracts.csproj packages/marketviewer-contracts/MarketViewer.Contracts/
COPY packages/marketviewer-core/MarketViewer.Core/MarketViewer.Core.csproj packages/marketviewer-core/MarketViewer.Core/
COPY packages/marketviewer-filters/MarketViewer.Filters/MarketViewer.Filters.csproj packages/marketviewer-filters/MarketViewer.Filters/
COPY packages/marketviewer-infrastructure/MarketViewer.Infrastructure/MarketViewer.Infrastructure.csproj packages/marketviewer-infrastructure/MarketViewer.Infrastructure/
COPY packages/marketviewer-studies/MarketViewer.Studies/MarketViewer.Studies.csproj packages/marketviewer-studies/MarketViewer.Studies/
COPY packages/massive-client/Massive.Client/Massive.Client.csproj packages/massive-client/Massive.Client/

RUN dotnet restore apps/backtester/Backtest.Lambda/Backtest.Lambda.csproj

COPY apps/backtester/Backtest.Lambda/ apps/backtester/Backtest.Lambda/
COPY packages/marketviewer-contracts/MarketViewer.Contracts/ packages/marketviewer-contracts/MarketViewer.Contracts/
COPY packages/marketviewer-core/MarketViewer.Core/ packages/marketviewer-core/MarketViewer.Core/
COPY packages/marketviewer-filters/MarketViewer.Filters/ packages/marketviewer-filters/MarketViewer.Filters/
COPY packages/marketviewer-infrastructure/MarketViewer.Infrastructure/ packages/marketviewer-infrastructure/MarketViewer.Infrastructure/
COPY packages/marketviewer-studies/MarketViewer.Studies/ packages/marketviewer-studies/MarketViewer.Studies/
COPY packages/massive-client/Massive.Client/ packages/massive-client/Massive.Client/

RUN dotnet publish apps/backtester/Backtest.Lambda/Backtest.Lambda.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o /app/publish \
    --no-restore

FROM public.ecr.aws/lambda/dotnet:10

ARG COMMIT_SHA=unknown
ENV COMMIT_SHA=$COMMIT_SHA

COPY --from=build /app/publish ${LAMBDA_TASK_ROOT}

CMD ["Backtest.Lambda::Backtest.Lambda.WorkerFunction::FunctionHandler"]
