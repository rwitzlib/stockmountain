using MarketViewer.Contracts.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Optimus.Infrastructure.Config;
using Optimus.Infrastructure.Repositories;
using System.Diagnostics.CodeAnalysis;

namespace Optimus.Infrastructure.DependencyInjection;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration.GetSection("UserConfig").Get<UserConfig>())
            .AddSingleton(configuration.GetSection("StrategyConfig").Get<StrategyConfig>())
            .AddSingleton(configuration.GetSection("TradeConfig").Get<TradeConfig>())
            .AddSingleton(configuration.GetSection("SqsConsumerConfig").Get<SqsConsumerConfig>())
            .AddSingleton(configuration.GetSection("ScanResultsConfig").Get<ScanConfig>())
            .AddSingleton(configuration.GetSection("ExecutionDedupConfig").Get<ExecutionDedupConfig>())
            .AddSingleton(configuration.GetSection("MetaConfig").Get<MetaConfig>());

        services.AddSingleton<TickerCache>();

        services.AddSingleton<StrategyRepository>()
            .AddSingleton<StrategyStateRepository>()
            .AddSingleton<TradeRepository>()
            .AddSingleton<UserRepository>()
            .AddSingleton<MetaRepository>()
            .AddSingleton<ScanResultsRepository>()
            .AddSingleton<ExecutionDedupRepository>();

        return services;
    }
}
