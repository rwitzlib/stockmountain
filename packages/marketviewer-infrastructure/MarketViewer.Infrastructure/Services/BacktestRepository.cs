using System.Text.Json;
using Microsoft.Extensions.Logging;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Document = Amazon.DynamoDBv2.DocumentModel.Document;
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
    public async Task<bool> Put(BacktestContextRecord record, IEnumerable<WorkerResponse> entries = null)
    {
        try
        {
            if (entries is not null)
            {
                var key = $"backtestResults/{record.UserId}/{record.Id}";

                var s3Response = await s3.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = config.S3BucketName,
                    Key = key,
                    ContentBody = JsonSerializer.Serialize(entries)
                });

                record.S3ObjectName = key;
            }

            var putRequest = new PutItemRequest
            {
                TableName = config.TableName,
                Item = MapContextRecordToAttributeMap(record)
            };
            var response = await dynamoDb.PutItemAsync(putRequest);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating backtest record");
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

            var record = MapAttributeMapToContextRecord(response.Item);
            return record;
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

    public async Task<List<WorkerResponse>> GetBacktestResultsFromS3(BacktestContextRecord record)
    {
        try
        {
            if (record is null || record.S3ObjectName is null)
            {
                return [];
            }

            var s3Response = await s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = config.S3BucketName,
                Key = record.S3ObjectName
            });

            using var streamReader = new StreamReader(s3Response.ResponseStream);
            var json = await streamReader.ReadToEndAsync();

            var s3Results = JsonSerializer.Deserialize<IEnumerable<WorkerResponse>>(json);
            s3Results.ToList().ForEach(q => q.CreditsUsed = 0);

            return s3Results.ToList();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving backtest results from S3 for record {recordId}", record.Id);
            return [];
        }
    }

    #region Private Methods

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
        var json = Document.FromAttributeMap(item).ToJson();
        var record = JsonSerializer.Deserialize<BacktestContextRecord>(json);

        if (string.IsNullOrEmpty(record.Id) && item.TryGetValue("PK", out var pk))
        {
            record.Id = pk.S;
        }

        return record;
    }

    #endregion
}
