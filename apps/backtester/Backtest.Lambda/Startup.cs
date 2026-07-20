using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.Logging.AspNetCore;
using Amazon.S3;
using Amazon.SQS;
using Backtest.Lambda;
using Backtest.Lambda.Services;
using DotNetEnv.Configuration;
using MarketViewer.Contracts.Caching;
using MarketViewer.Core.Services;
using MarketViewer.Filters;
using MarketViewer.Infrastructure.Config;
using MarketViewer.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Massive.Client.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

[assembly: LambdaSerializer(typeof(CustomSerializer))]

namespace Backtest.Lambda;

[ExcludeFromCodeCoverage]
public static class Startup
{
    private static readonly RegionEndpoint Region = RegionEndpoint.USEast2;

    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        var directory = Directory.GetCurrentDirectory();

        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddDotNetEnv("../../../../../docker.env")
            .AddEnvironmentVariables()
            .Build();

        var token = Environment.GetEnvironmentVariable("MASSIVE_TOKEN") ?? configuration.GetSection("Tokens").GetValue<string>("MassiveApi");

        var lambdaConfig = new AmazonLambdaConfig
        {
            Timeout = TimeSpan.FromMinutes(15),
            RegionEndpoint = Region
        };

        services.AddMemoryCache()
            //.AddScoped<IValidator<BacktestLambdaRequest>, BacktestRequestValidator>()
            .AddSingleton<IAmazonS3, AmazonS3Client>(client => new AmazonS3Client(Region))
            .AddSingleton<IAmazonLambda, AmazonLambdaClient>(client => new AmazonLambdaClient(lambdaConfig))
            .AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>(client => new AmazonDynamoDBClient(Region))
            .AddSingleton<IAmazonSQS, AmazonSQSClient>(client => new AmazonSQSClient(Region))
            .AddMassiveClient(token)
            .AddSingleton<ScannerService>()
            .AddSingleton<IMarketCache, MemoryMarketCache>()
            .AddSingleton<BacktestConfig>(configuration.GetSection("BacktestConfig").Get<BacktestConfig>())
            .AddSingleton<UserConfig>(configuration.GetSection("UserConfig").Get<UserConfig>())
            .AddSingleton<IUserRepository, UserRepository>()
            .AddSingleton<IndicatorExpressionEngine>()
            .AddSingleton<IBacktestRepository, BacktestRepository>()
            .AddSingleton<BacktestWorkerService>()
            .AddSingleton<DataCache>()
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
            loggingBuilder.AddLambdaLogger(new LambdaLoggerOptions
            {
                IncludeCategory = true,
                IncludeEventId = true,
                IncludeException = true,
                IncludeLogLevel = false,
                IncludeNewline = false,
                IncludeScopes = true
            });
        });
    }
}
