using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Lambda;
using Amazon.S3;
using DotNetEnv.Configuration;
using FluentValidation;
using MarketDataAggregator.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polygon.Client.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace MarketDataAggregator;

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

        var token = Environment.GetEnvironmentVariable("POLYGON_TOKEN") ?? configuration.GetSection("Tokens").GetValue<string>("PolygonApi");

        services.AddPolygonClient(token)
            .AddSingleton<IAmazonS3, AmazonS3Client>(_ => new AmazonS3Client(new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.USEast2
            }))
            .AddSingleton<IAmazonLambda, AmazonLambdaClient>(_ => new AmazonLambdaClient(RegionEndpoint.USEast2))
            .AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>(_ => new AmazonDynamoDBClient(RegionEndpoint.USEast2))
            .AddLogging()
            .AddSingleton<IValidator<MarketDataAggregatorRequest>, MarketDataAggregatorRequestValidator>()
            .AddSingleton<IValidator<MarketDataOrchestratorRequest>, MarketDataOrchestratorRequestValidator>();

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
