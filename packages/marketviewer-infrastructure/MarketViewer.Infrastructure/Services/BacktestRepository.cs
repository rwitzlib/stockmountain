using System.Text.Json;
using Microsoft.Extensions.Logging;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Document = Amazon.DynamoDBv2.DocumentModel.Document;
using MarketViewer.Contracts.Requests.Market.Backtest;
using MarketViewer.Contracts.Responses.Market.Backtest;
using MarketViewer.Core.Services;
using MarketViewer.Infrastructure.Config;
using MarketViewer.Contracts.Records.Backtest;

namespace MarketViewer.Infrastructure.Services;

public class BacktestRepository(
    BacktestConfig config,
    IAmazonDynamoDB dynamoDb,
    IAmazonS3 s3,
    ILogger<BacktestRepository> logger) : IBacktestRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<bool> Put(BacktestContextRecord record, IEnumerable<WorkerResponse> entries = null)
    {
        try
        {
            if (entries is not null)
            {
                var key = BuildUniverseKey(record);
                await PutS3JsonAsync(key, entries);
                record.S3ObjectName = key;
            }

            return await PutDynamoRecordAsync(record);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating backtest record");
            return false;
        }
    }

    public async Task<bool> PutCompleted(
        BacktestContextRecord record,
        BacktestResultResponse portfolio,
        IEnumerable<WorkerResponse> universe)
    {
        try
        {
            var universeKey = BuildUniverseKey(record);
            var portfolioKey = BuildPortfolioKey(record);

            await PutS3JsonAsync(universeKey, universe);
            await PutS3JsonAsync(portfolioKey, portfolio);

            record.S3ObjectName = universeKey;
            record.PortfolioS3ObjectName = portfolioKey;

            return await PutDynamoRecordAsync(record);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error persisting completed backtest artifacts for {id}", record.Id);
            return false;
        }
    }

    public async Task<BacktestContextRecord> Get(string id)
    {
        try
        {
            var response = await dynamoDb.GetItemAsync(new GetItemRequest
            {
                TableName = config.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = id } },
                    { "SK", new AttributeValue { S = "Context" } }
                }
            });

            if (response.Item == null || response.Item.Count == 0)
            {
                return null;
            }

            return MapAttributeMapToContextRecord(response.Item);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving backtest record with ID {id}", id);
            return null;
        }
    }

    public async Task<List<BacktestContextRecord>> List(string userId)
    {
        try
        {
            var response = await dynamoDb.QueryAsync(new QueryRequest
            {
                TableName = config.TableName,
                IndexName = config.UserIndexName,
                KeyConditionExpression = "UserId = :userId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":userId", new AttributeValue { S = userId } }
                }
            });

            return response.Items.Select(MapAttributeMapToContextRecord).ToList();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error listing backtest records for user {userId}", userId);
            return null;
        }
    }

    public async Task<BacktestResultResponse> GetPortfolioFromS3(BacktestContextRecord record)
    {
        try
        {
            if (string.IsNullOrEmpty(record.PortfolioS3ObjectName))
            {
                logger.LogWarning("No portfolio S3 key for backtest {recordId}", record.Id);
                return null;
            }

            var json = await GetS3JsonAsync(record.PortfolioS3ObjectName);
            return JsonSerializer.Deserialize<BacktestResultResponse>(json, SerializerOptions);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving portfolio from S3 for record {recordId}", record.Id);
            return null;
        }
    }

    public async Task<List<WorkerResponse>> GetUniverseFromS3(BacktestContextRecord record)
    {
        try
        {
            if (string.IsNullOrEmpty(record.S3ObjectName))
            {
                logger.LogWarning("No universe S3 key for backtest {recordId}", record.Id);
                return [];
            }

            var json = await GetS3JsonAsync(record.S3ObjectName);
            var s3Results = JsonSerializer.Deserialize<IEnumerable<WorkerResponse>>(json, SerializerOptions);
            var list = s3Results?.ToList() ?? [];
            list.ForEach(q => q.CreditsUsed = 0);
            return list;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving universe from S3 for record {recordId}", record.Id);
            return [];
        }
    }

    public Task<List<WorkerResponse>> GetBacktestResultsFromS3(BacktestContextRecord record) =>
        GetUniverseFromS3(record);

    #region Private Methods

    private static string BuildUniverseKey(BacktestContextRecord record) =>
        $"backtestResults/{record.UserId}/{record.Id}/universe.json";

    private static string BuildPortfolioKey(BacktestContextRecord record) =>
        $"backtestResults/{record.UserId}/{record.Id}/portfolio.json";

    private async Task PutS3JsonAsync<T>(string key, T payload)
    {
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = config.S3BucketName,
            Key = key,
            ContentBody = JsonSerializer.Serialize(payload, SerializerOptions)
        });
    }

    private async Task<string> GetS3JsonAsync(string key)
    {
        var s3Response = await s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = config.S3BucketName,
            Key = key
        });

        using var streamReader = new StreamReader(s3Response.ResponseStream);
        return await streamReader.ReadToEndAsync();
    }

    private async Task<bool> PutDynamoRecordAsync(BacktestContextRecord record)
    {
        var putRequest = new PutItemRequest
        {
            TableName = config.TableName,
            Item = MapContextRecordToAttributeMap(record)
        };
        await dynamoDb.PutItemAsync(putRequest);
        return true;
    }

    private static Dictionary<string, AttributeValue> MapContextRecordToAttributeMap(BacktestContextRecord record)
    {
        var attributeMap = new Dictionary<string, AttributeValue>
        {
            { "PK", new AttributeValue { S = record.Id } },
            { "SK", new AttributeValue { S = "Context" } },
            { "Id", new AttributeValue { S = record.Id } },
            { "UserId", new AttributeValue { S = record.UserId } },
            { "Status", new AttributeValue { S = record.Status.ToString() } },
            { "CreatedAt", new AttributeValue { S = record.CreatedAt } },
            { "Start", new AttributeValue { S = record.Start } },
            { "End", new AttributeValue { S = record.End } },
            { "Request", new AttributeValue { S = JsonSerializer.Serialize(record.Request) } },
            { "CreditsUsed", new AttributeValue { N = record.CreditsUsed.ToString() } },
            { "HoldProfit", new AttributeValue { N = record.HoldProfit.ToString() } },
            { "HighProfit", new AttributeValue { N = record.HighProfit.ToString() } },
            { "ConditionalProfit", new AttributeValue { N = record.ConditionalProfit.ToString() } },
            { "DurationSeconds", new AttributeValue { N = record.DurationSeconds.ToString() } }
        };

        if (!string.IsNullOrEmpty(record.S3ObjectName))
        {
            attributeMap.Add("S3ObjectName", new AttributeValue { S = record.S3ObjectName });
        }

        if (!string.IsNullOrEmpty(record.PortfolioS3ObjectName))
        {
            attributeMap.Add("PortfolioS3ObjectName", new AttributeValue { S = record.PortfolioS3ObjectName });
        }

        if (record.Errors != null && record.Errors.Count > 0)
        {
            attributeMap.Add("Errors", new AttributeValue { SS = record.Errors });
        }

        if (!string.IsNullOrEmpty(record.HoldStatsJson))
        {
            attributeMap.Add("HoldStatsJson", new AttributeValue { S = record.HoldStatsJson });
        }

        if (!string.IsNullOrEmpty(record.HighStatsJson))
        {
            attributeMap.Add("HighStatsJson", new AttributeValue { S = record.HighStatsJson });
        }

        return attributeMap;
    }

    private static BacktestContextRecord MapAttributeMapToContextRecord(Dictionary<string, AttributeValue> item)
    {
        // "Request" is stored as a serialized JSON string, but older records (written by the
        // lambda before consolidation) stored it as a native map, so handle both shapes.
        var attributes = item;
        item.TryGetValue("Request", out var requestAttribute);
        if (requestAttribute?.S is not null)
        {
            attributes = new Dictionary<string, AttributeValue>(item);
            attributes.Remove("Request");
        }

        var json = Document.FromAttributeMap(attributes).ToJson();
        var record = JsonSerializer.Deserialize<BacktestContextRecord>(json);

        if (requestAttribute?.S is not null)
        {
            record.Request = JsonSerializer.Deserialize<BacktestCreateRequest>(requestAttribute.S);
        }

        if (string.IsNullOrEmpty(record.Id) && item.TryGetValue("PK", out var pk))
        {
            record.Id = pk.S;
        }

        return record;
    }

    #endregion
}
