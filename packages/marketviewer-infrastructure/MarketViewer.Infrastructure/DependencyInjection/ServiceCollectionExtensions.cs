using Amazon;
using Amazon.Lambda;
using Amazon.S3;
using MarketViewer.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using MarketViewer.Contracts.Interfaces;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Polygon.Client.DependencyInjection;
using MarketViewer.Contracts.Caching;
using Amazon.DynamoDBv2;
using MarketViewer.Infrastructure.Config;
using MarketViewer.Core.Services;
using Amazon.DynamoDBv2.Model;

namespace MarketViewer.Infrastructure.DependencyInjection;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var token = configuration.GetSection("Tokens").GetValue<string>("PolygonApi");

        services.AddSingleton(configuration.GetSection("UserConfig").Get<UserConfig>());
        services.AddSingleton(configuration.GetSection("StrategyConfig").Get<StrategyConfig>());
        services.AddSingleton(configuration.GetSection("TradeConfig").Get<TradeConfig>());
        services.AddSingleton(configuration.GetSection("BacktestConfig").Get<BacktestConfig>());
        services.AddSingleton(configuration.GetSection("ScanConfig").Get<ScanConfig>());
        services.AddSingleton(configuration.GetSection("MetaConfig").Get<MetaConfig>());
        services.AddSingleton(configuration.GetSection("MarketDataConfig").Get<MarketDataConfig>());

        services.AddSingleton<IAmazonS3>(client => new AmazonS3Client(RegionEndpoint.USEast2))
            .AddPolygonClient(token)
            .AddSingleton<IMarketCache, MemoryMarketCache>()
            .AddSingleton<IAmazonLambda, AmazonLambdaClient>(_ => new AmazonLambdaClient(RegionEndpoint.USEast2))
            .AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>(client => new AmazonDynamoDBClient(RegionEndpoint.USEast2))
            .AddSingleton<IMarketDataRepository, MarketDataRepository>()
            .AddSingleton<ITradeRepository, TradeRepository>()
            .AddSingleton<IStrategyRepository, StrategyRepository>()
            .AddSingleton<IUserRepository, UserRepository>()
            .AddSingleton<IBacktestRepository, BacktestRepository>()
            .AddSingleton<IScanRepository, ScanRepository>()
            .AddSingleton<IMarketDataCatalogRepository, MarketDataCatalogRepository>()
            .AddSingleton<MetaRepository>();

        services.AddHostedService<StocksLiveFeed>();

        return services;
    }
}
