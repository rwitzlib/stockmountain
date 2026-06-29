using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.MarketData;
using MarketViewer.Contracts.Records.MarketData;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MarketDataAggregator;

public class MarketDataCatalogWriter(IAmazonDynamoDB dynamoDb, ILogger logger)
{
    private readonly string _tableName = Environment.GetEnvironmentVariable("MARKET_DATA_CATALOG_TABLE_NAME") ?? string.Empty;

    public async Task PutInventoryRecord(MarketDataInventoryRecord record)
    {
        if (string.IsNullOrWhiteSpace(_tableName))
        {
            return;
        }

        try
        {
            var item = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = MarketDataStorageContract.BuildInventoryPartitionKey(record.Date) } },
                { "SK", new AttributeValue { S = MarketDataStorageContract.BuildAggregateInventorySortKey(record.Multiplier, record.Timespan) } },
                { "Date", new AttributeValue { S = record.Date.Date.ToString("yyyy-MM-dd") } },
                { "Multiplier", new AttributeValue { N = record.Multiplier.ToString() } },
                { "Timespan", new AttributeValue { S = record.Timespan.ToString() } },
                { "Bucket", new AttributeValue { S = record.Bucket ?? string.Empty } },
                { "Key", new AttributeValue { S = record.Key ?? string.Empty } },
                { "Status", new AttributeValue { S = record.Status.ToString() } },
                { "Source", new AttributeValue { S = record.Source ?? string.Empty } }
            };

            AddOptional(item, "ETag", record.ETag);
            AddOptional(item, "RunId", record.RunId);
            AddOptional(item, "Error", record.Error);
            AddOptional(item, "ObjectSize", record.ObjectSize);
            AddOptional(item, "RecordCount", record.RecordCount);
            AddOptional(item, "StartedAt", record.StartedAt);
            AddOptional(item, "CompletedAt", record.CompletedAt);

            await dynamoDb.PutItemAsync(new PutItemRequest
            {
                TableName = _tableName,
                Item = item
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write market data inventory record for {Date} {Multiplier}/{Timespan}.", record.Date, record.Multiplier, record.Timespan);
        }
    }

    public async Task PutRunRecord(MarketDataRunRecord record)
    {
        if (string.IsNullOrWhiteSpace(_tableName))
        {
            return;
        }

        try
        {
            var item = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = MarketDataStorageContract.BuildRunPartitionKey() } },
                { "SK", new AttributeValue { S = MarketDataStorageContract.BuildRunSortKey(record.RunId) } },
                { "RunId", new AttributeValue { S = record.RunId } },
                { "Start", new AttributeValue { S = record.Start.Date.ToString("yyyy-MM-dd") } },
                { "End", new AttributeValue { S = record.End.Date.ToString("yyyy-MM-dd") } },
                { "Timespans", new AttributeValue { S = JsonSerializer.Serialize(record.Timespans) } },
                { "Multiplier", new AttributeValue { N = record.Multiplier.ToString() } },
                { "Status", new AttributeValue { S = record.Status.ToString() } },
                { "Source", new AttributeValue { S = record.Source ?? string.Empty } },
                { "RequestedCount", new AttributeValue { N = record.RequestedCount.ToString() } },
                { "CompletedCount", new AttributeValue { N = record.CompletedCount.ToString() } },
                { "FailedCount", new AttributeValue { N = record.FailedCount.ToString() } },
                { "StartedAt", new AttributeValue { S = record.StartedAt.ToString("O") } }
            };

            AddOptional(item, "CompletedAt", record.CompletedAt);
            AddOptional(item, "Error", record.Error);

            await dynamoDb.PutItemAsync(new PutItemRequest
            {
                TableName = _tableName,
                Item = item
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write market data run record {RunId}.", record.RunId);
        }
    }

    private static void AddOptional(Dictionary<string, AttributeValue> item, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            item[name] = new AttributeValue { S = value };
        }
    }

    private static void AddOptional(Dictionary<string, AttributeValue> item, string name, long? value)
    {
        if (value is not null)
        {
            item[name] = new AttributeValue { N = value.Value.ToString() };
        }
    }

    private static void AddOptional(Dictionary<string, AttributeValue> item, string name, int? value)
    {
        if (value is not null)
        {
            item[name] = new AttributeValue { N = value.Value.ToString() };
        }
    }

    private static void AddOptional(Dictionary<string, AttributeValue> item, string name, DateTimeOffset? value)
    {
        if (value is not null)
        {
            item[name] = new AttributeValue { S = value.Value.ToString("O") };
        }
    }
}
