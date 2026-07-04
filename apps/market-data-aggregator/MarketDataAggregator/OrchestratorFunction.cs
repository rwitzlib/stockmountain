using Amazon.DynamoDBv2;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.Model;
using FluentValidation;
using MarketDataAggregator.Validation;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.MarketData;
using MarketViewer.Contracts.Records.MarketData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MarketDataAggregator;

public class OrchestratorFunction(IServiceProvider serviceProvider)
{
    private readonly IAmazonLambda _lambda = serviceProvider.GetRequiredService<IAmazonLambda>();
    private readonly IAmazonDynamoDB _dynamoDb = serviceProvider.GetRequiredService<IAmazonDynamoDB>();
    private readonly ILogger<OrchestratorFunction> _logger = serviceProvider.GetRequiredService<ILogger<OrchestratorFunction>>();
    private readonly IValidator<MarketDataOrchestratorRequest> _requestValidator = serviceProvider.GetRequiredService<IValidator<MarketDataOrchestratorRequest>>();
    private readonly string _aggregatorFunctionName = System.Environment.GetEnvironmentVariable("MARKET_DATA_AGGREGATOR_FUNCTION_NAME") ?? string.Empty;

    public OrchestratorFunction() : this(Startup.ConfigureServices()) { }

    public async Task FunctionHandler(MarketDataOrchestratorRequest request, ILambdaContext context)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var catalog = new MarketDataCatalogWriter(_dynamoDb, _logger);

        if (request is null)
        {
            _logger.LogInformation("Invalid request. Request is required.");
            await WriteFailedRunRecord(catalog, null, startedAt, "Invalid market data orchestrator request.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_aggregatorFunctionName))
        {
            _logger.LogInformation("Invalid configuration. MARKET_DATA_AGGREGATOR_FUNCTION_NAME is required.");
            await WriteFailedRunRecord(catalog, request, startedAt, "Invalid market data orchestrator request.");
            return;
        }

        MarketDataRequestNormalizer.NormalizeOrchestratorRequest(request);

        var validationResult = _requestValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            LogValidationErrors(validationResult);
            await WriteFailedRunRecord(catalog, request, startedAt, "Invalid market data orchestrator request.");
            return;
        }

        var workItems = BuildWorkItems(request).ToList();
        await catalog.PutRunRecord(new MarketDataRunRecord
        {
            RunId = request.RunId,
            Start = request.Start,
            End = request.End,
            Timespans = request.Timespans,
            Multiplier = request.Multiplier,
            Source = request.Source,
            Status = MarketDataStatus.Running,
            RequestedCount = workItems.Count,
            StartedAt = startedAt
        });

        var completed = 0;
        var failed = 0;
        using var semaphore = new SemaphoreSlim(request.MaxConcurrency);

        var tasks = workItems.Select(async item =>
        {
            await semaphore.WaitAsync();
            try
            {
                await InvokeAggregator(item);
                Interlocked.Increment(ref completed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to invoke market data aggregator for {Date} {Timespan}.", item.Date, item.Timespan);
                Interlocked.Increment(ref failed);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        await catalog.PutRunRecord(new MarketDataRunRecord
        {
            RunId = request.RunId,
            Start = request.Start,
            End = request.End,
            Timespans = request.Timespans,
            Multiplier = request.Multiplier,
            Source = request.Source,
            Status = failed > 0 ? MarketDataStatus.Failed : MarketDataStatus.Succeeded,
            RequestedCount = workItems.Count,
            CompletedCount = completed,
            FailedCount = failed,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.UtcNow
        });
    }

    private async Task WriteFailedRunRecord(
        MarketDataCatalogWriter catalog,
        MarketDataOrchestratorRequest? request,
        DateTimeOffset startedAt,
        string error)
    {
        await catalog.PutRunRecord(new MarketDataRunRecord
        {
            RunId = request?.RunId ?? Guid.NewGuid().ToString("N"),
            Start = request?.Start ?? DateTimeOffset.UtcNow,
            End = request?.End ?? DateTimeOffset.UtcNow,
            Timespans = request?.Timespans ?? [],
            Multiplier = request?.Multiplier ?? 1,
            Source = request?.Source ?? "backfill",
            Status = MarketDataStatus.Failed,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.UtcNow,
            Error = error
        });
    }

    private void LogValidationErrors(FluentValidation.Results.ValidationResult validationResult)
    {
        foreach (var error in validationResult.Errors)
        {
            _logger.LogInformation("Invalid request. {Message}", error.ErrorMessage);
        }
    }

    private static IEnumerable<MarketDataAggregatorRequest> BuildWorkItems(MarketDataOrchestratorRequest request)
    {
        return MarketDataWorkPlanner.BuildWorkDates(request.Start, request.End, request.Timespans)
            .Select(item => new MarketDataAggregatorRequest
            {
                Date = item.Date,
                Multiplier = request.Multiplier,
                Timespan = item.Timespan,
                RunId = request.RunId,
                Source = request.Source,
                Overwrite = request.Overwrite
            });
    }

    private async Task InvokeAggregator(MarketDataAggregatorRequest request)
    {
        await _lambda.InvokeAsync(new InvokeRequest
        {
            FunctionName = _aggregatorFunctionName,
            InvocationType = InvocationType.Event,
            Payload = JsonSerializer.Serialize(request)
        });
    }
}
