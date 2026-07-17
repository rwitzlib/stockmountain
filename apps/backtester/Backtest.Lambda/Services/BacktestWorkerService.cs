using Amazon.Lambda;
using Amazon.Lambda.Model;
using MarketViewer.Contracts.Requests.Market.Backtest;
using MarketViewer.Contracts.Responses.Market.Backtest;
using MarketViewer.Infrastructure.Config;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Environment = System.Environment;

namespace Backtest.Lambda.Services;

/// <summary>
/// Fans a backtest request out to the worker lambda, one invocation per trading day.
/// </summary>
public class BacktestWorkerService(
    BacktestConfig config,
    IAmazonLambda lambda,
    ILogger<BacktestWorkerService> logger)
{
    public async Task<List<WorkerResponse>> GetBacktestResultsFromLambda(OrchestratorRequest request)
    {
        var days = request.End == request.Start ? [request.Start] : Enumerable.Range(0, (request.End - request.Start).Days + 1)
            .Select(day => request.Start.AddDays(day))
            .Where(day => day.DayOfWeek != DayOfWeek.Sunday && day.DayOfWeek != DayOfWeek.Saturday);

        logger.LogInformation("Backtesting strategy between {start} and {end}. Total days: {count}",
            request.Start.ToString("yyyy-MM-dd"),
            request.End.ToString("yyyy-MM-dd"),
            days.Count());

        int batchSize = int.TryParse(Environment.GetEnvironmentVariable("WORKER_BATCH_SIZE"), out var workerBatchSize) ? workerBatchSize : 100;
        var lambdaResults = new List<WorkerResponse>();

        for (int i = 0; i < days.Count(); i += batchSize)
        {
            var batch = days.Skip(i).Take(batchSize);
            var tasks = batch.Select(day =>
            {
                var backtesterLambdaRequest = new WorkerRequest
                {
                    BacktestId = request.Id,
                    Date = day.Date,
                    PositionSettings = request.PositionSettings,
                    EntrySettings = request.EntrySettings,
                    ExitSettings = request.ExitSettings
                };
                return Task.Run(async () => await BacktestDay(backtesterLambdaRequest));
            }).ToList();

            var taskResults = await Task.WhenAll(tasks);
            var batchResults = taskResults.Where(q => q is not null && q.Results is not null);
            lambdaResults.AddRange(batchResults);
        }

        return lambdaResults.ToList();
    }

    #region Private Methods

    private async Task<WorkerResponse> BacktestDay(WorkerRequest request)
    {
        const int MaxAttempts = 3;
        string lastError = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var json = JsonSerializer.Serialize(request);

                var invokeRequest = new InvokeRequest
                {
                    FunctionName = config.LambdaName,
                    InvocationType = InvocationType.RequestResponse,
                    Payload = json,
                };

                var response = await lambda.InvokeAsync(invokeRequest);

                // FunctionError is set when the worker itself crashed (timeout, OOM) —
                // the payload is then an error document, not a WorkerResponse.
                if (!string.IsNullOrEmpty(response.FunctionError))
                {
                    lastError = $"worker crashed ({response.FunctionError}): {ReadPayload(response)}";
                }
                else if (response.Payload is null)
                {
                    lastError = "empty response from worker";
                }
                else
                {
                    var backtestEntry = JsonSerializer.Deserialize<WorkerResponse>(ReadPayload(response));

                    if (backtestEntry is not null)
                    {
                        return backtestEntry;
                    }

                    lastError = "unreadable response from worker";
                }
            }
            catch (Exception e)
            {
                lastError = e.Message;
                logger.LogError(e, "Error backtesting day {date} (attempt {attempt}/{maxAttempts})", request.Date, attempt, MaxAttempts);
            }

            if (attempt < MaxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500)));
            }
        }

        logger.LogError("Day {date} failed after {maxAttempts} attempts: {error}", request.Date, MaxAttempts, lastError);

        // Return a placeholder instead of null so the failure is surfaced on the
        // backtest record rather than the day silently vanishing from the results.
        return new WorkerResponse
        {
            Date = request.Date.Date,
            Results = [],
            Errors = [$"Day could not be backtested after {MaxAttempts} attempts: {Truncate(lastError, 300)}"]
        };
    }

    private static string ReadPayload(InvokeResponse response)
    {
        if (response.Payload is null)
        {
            return string.Empty;
        }

        using var streamReader = new StreamReader(response.Payload);
        return streamReader.ReadToEnd();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...";
    }

    #endregion
}
