FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY apps/backtest-worker/Backtest.Lambda/Backtest.Lambda.csproj apps/backtest-worker/Backtest.Lambda/
COPY packages/marketviewer-contracts/MarketViewer.Contracts/MarketViewer.Contracts.csproj packages/marketviewer-contracts/MarketViewer.Contracts/
COPY packages/marketviewer-filters/MarketViewer.Filters/MarketViewer.Filters.csproj packages/marketviewer-filters/MarketViewer.Filters/
COPY packages/marketviewer-studies/MarketViewer.Studies/MarketViewer.Studies.csproj packages/marketviewer-studies/MarketViewer.Studies/

RUN dotnet restore apps/backtest-worker/Backtest.Lambda/Backtest.Lambda.csproj

COPY apps/backtest-worker/Backtest.Lambda/ apps/backtest-worker/Backtest.Lambda/
COPY packages/marketviewer-contracts/MarketViewer.Contracts/ packages/marketviewer-contracts/MarketViewer.Contracts/
COPY packages/marketviewer-filters/MarketViewer.Filters/ packages/marketviewer-filters/MarketViewer.Filters/
COPY packages/marketviewer-studies/MarketViewer.Studies/ packages/marketviewer-studies/MarketViewer.Studies/

RUN dotnet publish apps/backtest-worker/Backtest.Lambda/Backtest.Lambda.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o /app/publish \
    --no-restore

FROM public.ecr.aws/lambda/dotnet:10

COPY --from=build /app/publish ${LAMBDA_TASK_ROOT}

CMD ["Backtest.Lambda::Backtest.Lambda.WorkerFunction::FunctionHandler"]
