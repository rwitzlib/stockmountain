FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY apps/market-data-aggregator/MarketDataAggregator/MarketDataAggregator.csproj apps/market-data-aggregator/MarketDataAggregator/
COPY packages/marketviewer-contracts/MarketViewer.Contracts/MarketViewer.Contracts.csproj packages/marketviewer-contracts/MarketViewer.Contracts/

RUN dotnet restore apps/market-data-aggregator/MarketDataAggregator/MarketDataAggregator.csproj

COPY apps/market-data-aggregator/MarketDataAggregator/ apps/market-data-aggregator/MarketDataAggregator/
COPY packages/marketviewer-contracts/MarketViewer.Contracts/ packages/marketviewer-contracts/MarketViewer.Contracts/

RUN dotnet publish apps/market-data-aggregator/MarketDataAggregator/MarketDataAggregator.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o /app/publish \
    --no-restore

FROM public.ecr.aws/lambda/dotnet:10

COPY --from=build /app/publish ${LAMBDA_TASK_ROOT}

CMD ["MarketDataAggregator::MarketDataAggregator.AggregatorFunction::FunctionHandler"]
