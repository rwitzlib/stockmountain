using Amazon;
using Amazon.DynamoDBv2;
using Amazon.S3;
using Backtest.Lambda.Services;
using DotNetEnv.Configuration;
using MarketViewer.Contracts.Caching;
using MarketViewer.Filters;
using Polygon.Client.DependencyInjection;


var builder = WebApplication.CreateBuilder(args);

RegionEndpoint Region = RegionEndpoint.USEast2;

// Add services to the container.

var directory = Directory.GetCurrentDirectory();
var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "local";

var configuration = new ConfigurationBuilder()
    .AddJsonFile($"appsettings.{environment}.json", optional: true)
    .AddDotNetEnv("../../../../../docker.env")
    .AddEnvironmentVariables()
    .Build();

var token = Environment.GetEnvironmentVariable("POLYGON_TOKEN") ?? configuration.GetSection("Tokens").GetValue<string>("PolygonApi");

builder.Services.AddMemoryCache()
    .AddSingleton<IAmazonS3, AmazonS3Client>(client => new AmazonS3Client(Region))
    .AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>(client => new AmazonDynamoDBClient(Region))
    .AddPolygonClient(token)
    .AddSingleton<ScannerService>()
    .AddSingleton<IMarketCache, MemoryMarketCache>()
    .AddSingleton<IndicatorExpressionEngine>()
    .AddSingleton<DataCache>()
    .AddLogging();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
