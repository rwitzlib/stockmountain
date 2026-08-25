using Amazon;
using Amazon.DynamoDBv2;
using DotNetEnv.Configuration;
using MarketViewer.Core.Services;
using MarketViewer.Infrastructure.Config;
using MarketViewer.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Billing.Lambda;

[ExcludeFromCodeCoverage]
public static class Startup
{
    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddDotNetEnv("../../../../docker.env")
            .AddEnvironmentVariables()
            .Build();

        services
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>(_ => new AmazonDynamoDBClient(RegionEndpoint.USEast2))
            .AddSingleton<UserConfig>(configuration.GetSection("UserConfig").Get<UserConfig>())
            .AddSingleton<BillingLedgerConfig>(configuration.GetSection("BillingLedgerConfig").Get<BillingLedgerConfig>())
            .AddSingleton<IBillingLedgerRepository, BillingLedgerRepository>()
            .AddSingleton<MonthlyRefillService>()
            .AddLogging();

        services.ConfigureLogging(configuration);

        return services.BuildServiceProvider();
    }

    private static void ConfigureLogging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
            loggingBuilder.AddJsonConsole();
        });
    }
}
