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
            .AddSingleton<IAmazonLambda, AmazonLambdaClient>(client => new AmazonLambdaClient(new AmazonLambdaConfig
            {
                Timeout = TimeSpan.FromMinutes(5),
                RegionEndpoint = RegionEndpoint.USEast2
            }))
            .AddScoped<SnapshotHandler>()
            .AddScoped<BacktestHandler>()
            .AddScoped<MarketDataHandler>()
            .AddSingleton<TickerHandler>()
            .AddSingleton<PerformanceHandler>();
    }
}
