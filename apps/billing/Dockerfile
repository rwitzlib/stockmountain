FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY apps/billing/Billing.Lambda/Billing.Lambda.csproj apps/billing/Billing.Lambda/
COPY packages/marketviewer-contracts/MarketViewer.Contracts/MarketViewer.Contracts.csproj packages/marketviewer-contracts/MarketViewer.Contracts/
COPY packages/marketviewer-core/MarketViewer.Core/MarketViewer.Core.csproj packages/marketviewer-core/MarketViewer.Core/
COPY packages/marketviewer-infrastructure/MarketViewer.Infrastructure/MarketViewer.Infrastructure.csproj packages/marketviewer-infrastructure/MarketViewer.Infrastructure/
COPY packages/massive-client/Massive.Client/Massive.Client.csproj packages/massive-client/Massive.Client/

RUN dotnet restore apps/billing/Billing.Lambda/Billing.Lambda.csproj

COPY apps/billing/Billing.Lambda/ apps/billing/Billing.Lambda/
COPY packages/marketviewer-contracts/MarketViewer.Contracts/ packages/marketviewer-contracts/MarketViewer.Contracts/
COPY packages/marketviewer-core/MarketViewer.Core/ packages/marketviewer-core/MarketViewer.Core/
COPY packages/marketviewer-infrastructure/MarketViewer.Infrastructure/ packages/marketviewer-infrastructure/MarketViewer.Infrastructure/
COPY packages/massive-client/Massive.Client/ packages/massive-client/Massive.Client/

RUN dotnet publish apps/billing/Billing.Lambda/Billing.Lambda.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o /app/publish \
    --no-restore

FROM public.ecr.aws/lambda/dotnet:10

ARG COMMIT_SHA=unknown
ENV COMMIT_SHA=$COMMIT_SHA

COPY --from=build /app/publish ${LAMBDA_TASK_ROOT}

CMD ["Billing.Lambda::Billing.Lambda.RefillFunction::FunctionHandler"]
