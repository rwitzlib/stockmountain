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

            if (response.Payload is null)
            {
                return null;
            }

            var streamReader = new StreamReader(response.Payload);
            var result = streamReader.ReadToEnd();

            var backtestEntry = JsonSerializer.Deserialize<WorkerResponse>(result);

            return backtestEntry;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error backtesting day {date}", request.Date);
            return null;
        }
    }

    #endregion
}
