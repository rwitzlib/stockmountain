using Amazon;
using Amazon.Lambda;
using FluentValidation;
using MarketViewer.Application.Handlers.Market.Backtest;
using MarketViewer.Application.Services;
using MarketViewer.Application.Handlers.Tools;
using MarketViewer.Application.Validators;
using MarketViewer.Contracts.Requests.Market.Backtest;
using MarketViewer.Contracts.Requests.Management.Strategy;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics.CodeAnalysis;
using MarketViewer.Application.Handlers.Data.Tickers;
using MarketViewer.Application.Handlers.Management.Strategy;
using MarketViewer.Application.Handlers.Management.Trade;
using MarketViewer.Application.Handlers.Management.User;
using MarketViewer.Application.Handlers.Market;
using MarketViewer.Application.Handlers.Market.Scan;
using MarketViewer.Application.Handlers.Market.Tools;
using MarketViewer.Application.Handlers.MarketData;

namespace MarketViewer.Application.DependencyInjection;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterApplication(this IServiceCollection services)
    {
        return services.AddScoped<IValidator<BacktestCreateRequest>, BacktestRequestValidator>()
            .AddScoped<IValidator<StrategyCreateRequest>, StrategyCreateRequestValidator>()
            .AddScoped<IValidator<StrategyUpdateRequest>, StrategyUpdateRequestValidator>()
            .AddSingleton<IIndicatorCalculationService, IndicatorCalculationService>()
            .AddSingleton<IGpuSmaCalculationService, GpuSmaCalculationService>()
            .AddScoped<ClerkUserProvisioningService>()
            .AddSingleton<IAmazonLambda, AmazonLambdaClient>(client => new AmazonLambdaClient(new AmazonLambdaConfig
            {
                Timeout = TimeSpan.FromMinutes(5),
                RegionEndpoint = RegionEndpoint.USEast2
            }))
            .AddScoped<SnapshotHandler>()
            .AddScoped<BacktestHandler>()
            .AddScoped<BacktestShareHandler>()
            .AddScoped<MarketDataHandler>()
            .AddScoped<StocksHandler>()
            .AddScoped<ScanHandler>()
            .AddScoped<ToolsFilterHandler>()
            .AddScoped<StrategyCreateHandler>()
            .AddScoped<StrategyReadHandler>()
            .AddScoped<StrategyListHandler>()
            .AddScoped<StrategyUpdateHandler>()
            .AddScoped<StrategyDeleteHandler>()
            .AddScoped<StrategyOptimizeHandler>()
            .AddScoped<StrategyStateHandler>()
            .AddScoped<BalanceHistoryHandler>()
            .AddScoped<TradeOpenHandler>()
            .AddScoped<TradeCloseHandler>()
            .AddScoped<TradeListHandler>()
            .AddScoped<UserReadHandler>()
            .AddSingleton<TickerHandler>()
            .AddSingleton<PerformanceHandler>();
    }
}
