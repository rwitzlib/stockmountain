using Amazon.DynamoDBv2;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.Model;
using MarketViewer.Contracts.Enums;
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
    private readonly string _aggregatorFunctionName = System.Environment.GetEnvironmentVariable("MARKET_DATA_AGGREGATOR_FUNCTION_NAME") ?? string.Empty;

    public OrchestratorFunction() : this(Startup.ConfigureServices()) { }

    public async Task FunctionHandler(MarketDataOrchestratorRequest request, ILambdaContext context)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var catalog = new MarketDataCatalogWriter(_dynamoDb, _logger);

        if (!IsRequestValid(request))
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
                Error = "Invalid market data orchestrator request."
            });
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

    private bool IsRequestValid(MarketDataOrchestratorRequest request)
    {
        if (request is null)
        {
            _logger.LogInformation("Invalid request. Request is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(_aggregatorFunctionName))
        {
            _logger.LogInformation("Invalid configuration. MARKET_DATA_AGGREGATOR_FUNCTION_NAME is required.");
            return false;
        }

        if (request.Start.Date > request.End.Date)
        {
            _logger.LogInformation("Invalid request. Start must be before or equal to end.");
            return false;
        }

        if (request.Multiplier <= 0 || request.Multiplier > 30)
        {
            _logger.LogInformation("Invalid request. Multiplier must be between 1 and 30.");
            return false;
        }

        if (request.MaxConcurrency <= 0)
        {
            request.MaxConcurrency = 1;
        }

        request.Timespans = request.Timespans
            .Where(timespan => timespan is Timespan.minute or Timespan.hour or Timespan.day)
            .Distinct()
            .ToList();

        return request.Timespans.Count > 0;
    }

    private static IEnumerable<MarketDataAggregatorRequest> BuildWorkItems(MarketDataOrchestratorRequest request)
    {
        var days = (request.End.Date - request.Start.Date).Days;
        for (var i = 0; i <= days; i++)
        {
            var date = request.Start.Date.AddDays(i);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            foreach (var timespan in request.Timespans)
            {
                yield return new MarketDataAggregatorRequest
                {
                    Date = date,
                    Multiplier = request.Multiplier,
                    Timespan = timespan,
                    RunId = request.RunId,
                    Source = request.Source,
                    Overwrite = request.Overwrite
                };
            }
        }
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
