FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY Directory.Build.props .
COPY apps/api/MarketViewer.Api/MarketViewer.Api.csproj apps/api/MarketViewer.Api/
COPY packages/marketviewer-application/MarketViewer.Application/MarketViewer.Application.csproj packages/marketviewer-application/MarketViewer.Application/
COPY packages/marketviewer-contracts/MarketViewer.Contracts/MarketViewer.Contracts.csproj packages/marketviewer-contracts/MarketViewer.Contracts/
COPY packages/marketviewer-core/MarketViewer.Core/MarketViewer.Core.csproj packages/marketviewer-core/MarketViewer.Core/
COPY packages/marketviewer-filters/MarketViewer.Filters/MarketViewer.Filters.csproj packages/marketviewer-filters/MarketViewer.Filters/
COPY packages/marketviewer-infrastructure/MarketViewer.Infrastructure/MarketViewer.Infrastructure.csproj packages/marketviewer-infrastructure/MarketViewer.Infrastructure/
COPY packages/marketviewer-studies/MarketViewer.Studies/MarketViewer.Studies.csproj packages/marketviewer-studies/MarketViewer.Studies/

RUN dotnet restore apps/api/MarketViewer.Api/MarketViewer.Api.csproj

COPY apps/api/MarketViewer.Api/ apps/api/MarketViewer.Api/
COPY packages/marketviewer-application/MarketViewer.Application/ packages/marketviewer-application/MarketViewer.Application/
COPY packages/marketviewer-contracts/MarketViewer.Contracts/ packages/marketviewer-contracts/MarketViewer.Contracts/
COPY packages/marketviewer-core/MarketViewer.Core/ packages/marketviewer-core/MarketViewer.Core/
COPY packages/marketviewer-filters/MarketViewer.Filters/ packages/marketviewer-filters/MarketViewer.Filters/
COPY packages/marketviewer-infrastructure/MarketViewer.Infrastructure/ packages/marketviewer-infrastructure/MarketViewer.Infrastructure/
COPY packages/marketviewer-studies/MarketViewer.Studies/ packages/marketviewer-studies/MarketViewer.Studies/

RUN dotnet publish apps/api/MarketViewer.Api/MarketViewer.Api.csproj \
    -c Release \
    --self-contained false \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

RUN apk add --no-cache tzdata

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "MarketViewer.Api.dll"]
