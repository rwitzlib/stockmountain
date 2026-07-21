FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY Directory.Build.props .
COPY apps/paper-bot-runner/Optimus/Optimus.csproj apps/paper-bot-runner/Optimus/
COPY packages/alpaca-client/Alpaca.Client/Alpaca.Client.csproj packages/alpaca-client/Alpaca.Client/
COPY packages/marketviewer-clients/MarketViewer.Clients/MarketViewer.Clients.csproj packages/marketviewer-clients/MarketViewer.Clients/
COPY packages/marketviewer-contracts/MarketViewer.Contracts/MarketViewer.Contracts.csproj packages/marketviewer-contracts/MarketViewer.Contracts/
COPY packages/massive-client/Massive.Client/Massive.Client.csproj packages/massive-client/Massive.Client/
COPY packages/optimus-adapter/Optimus.Adapter/Optimus.Adapter.csproj packages/optimus-adapter/Optimus.Adapter/
COPY packages/optimus-infrastructure/Optimus.Infrastructure/Optimus.Infrastructure.csproj packages/optimus-infrastructure/Optimus.Infrastructure/
COPY packages/schwab-api/SchwabApi/SchwabApi.csproj packages/schwab-api/SchwabApi/

RUN dotnet restore apps/paper-bot-runner/Optimus/Optimus.csproj

COPY apps/paper-bot-runner/Optimus/ apps/paper-bot-runner/Optimus/
COPY packages/alpaca-client/Alpaca.Client/ packages/alpaca-client/Alpaca.Client/
COPY packages/marketviewer-clients/MarketViewer.Clients/ packages/marketviewer-clients/MarketViewer.Clients/
COPY packages/marketviewer-contracts/MarketViewer.Contracts/ packages/marketviewer-contracts/MarketViewer.Contracts/
COPY packages/massive-client/Massive.Client/ packages/massive-client/Massive.Client/
COPY packages/optimus-adapter/Optimus.Adapter/ packages/optimus-adapter/Optimus.Adapter/
COPY packages/optimus-infrastructure/Optimus.Infrastructure/ packages/optimus-infrastructure/Optimus.Infrastructure/
COPY packages/schwab-api/SchwabApi/ packages/schwab-api/SchwabApi/

RUN dotnet publish apps/paper-bot-runner/Optimus/Optimus.csproj \
    -c Release \
    --self-contained false \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

RUN apk add --no-cache tzdata

ARG COMMIT_SHA=unknown
ENV COMMIT_SHA=$COMMIT_SHA

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Optimus.dll"]
