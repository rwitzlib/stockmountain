using Amazon.Lambda.Model;
using Amazon.Lambda;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Document = Amazon.DynamoDBv2.DocumentModel.Document;
using MarketViewer.Contracts.Responses.Market.Backtest;
using MarketViewer.Contracts.Requests.Market.Backtest;
using Backtest.Lambda.Config;
using MarketViewer.Contracts.Records.Backtest;
using System.Net;
using Environment = System.Environment;

namespace Backtest.Lambda.Repository;

public class BacktestRepository(
    BacktestConfig config,
    IAmazonDynamoDB dynamoDb,
    IAmazonLambda lambda,
    IAmazonS3 s3,
    ILogger<BacktestRepository> logger)
{
    public async Task<BacktestContextRecord> Get(string id)
    {
        try
        {
            var queryResponse = await dynamoDb.GetItemAsync(new GetItemRequest
            {
                TableName = config.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "Id", new AttributeValue { S = id } }
                }
            });

            if (queryResponse.HttpStatusCode != HttpStatusCode.OK || queryResponse.Item == null || !queryResponse.IsItemSet)
            {
                return null;
            }

            var json = Document.FromAttributeMap(queryResponse.Item).ToJson();
            return JsonSerializer.Deserialize<BacktestContextRecord>(json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting backtest record with ID: {Id}", id);
            return null;
        }
    }

    public async Task<bool> Put(BacktestContextRecord record, IEnumerable<WorkerResponse> responses = null)
    {
        try
        {
            var key = $"backtestResults/{record.UserId}/{record.Id}";
            if (responses is not null)
            {
                var putObjectRequest = new PutObjectRequest
                {
                    BucketName = config.S3BucketName,
                    Key = key,
                    ContentBody = JsonSerializer.Serialize(responses)
                };

                var putObjectResponse = await s3.PutObjectAsync(putObjectRequest);

                if (putObjectResponse.HttpStatusCode != HttpStatusCode.OK)
                {
                    logger.LogError("Failed to put backtest results to S3 for backtest ID: {Id}", record.Id);
                    return false;
                }

                record.S3ObjectName = key;
            }

            var putRequest = new PutItemRequest
            {
                TableName = config.TableName,
                Item = Document.FromJson(JsonSerializer.Serialize(record)).ToAttributeMap()
            };

            var response = await dynamoDb.PutItemAsync(putRequest);

            if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
            {
                logger.LogError("Failed to put backtest record with ID: {Id}", record.Id);
                return false;
            }

            logger.LogInformation("Successfully created backtest record with ID: {Id}", record.Id);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating backtest record");
            return false;
        }
    }

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
            return null;
        }
    }

    #endregion
}
