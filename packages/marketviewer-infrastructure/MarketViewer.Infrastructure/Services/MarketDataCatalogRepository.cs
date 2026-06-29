using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Interfaces;
using MarketViewer.Contracts.MarketData;
using MarketViewer.Contracts.Records.MarketData;
using MarketViewer.Contracts.Requests.MarketData;
using MarketViewer.Infrastructure.Config;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace MarketViewer.Infrastructure.Services;

public class MarketDataCatalogRepository(
    MarketDataConfig config,
    IAmazonDynamoDB dynamoDb,
    ILogger<MarketDataCatalogRepository> logger) : IMarketDataCatalogRepository
{
    public async Task<MarketDataInventoryRecord> PutInventoryRecord(MarketDataInventoryRecord record)
    {
        var response = await dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = config.TableName,
            Item = MapInventory(record)
        });

        if (response.HttpStatusCode != HttpStatusCode.OK)
        {
            logger.LogError("Failed to put market data inventory record for {Date} {Multiplier}/{Timespan}.", record.Date, record.Multiplier, record.Timespan);
        }

        return record;
    }

    public async Task<List<MarketDataInventoryRecord>> ListInventory(MarketDataInventoryQueryRequest request)
    {
        var records = new List<MarketDataInventoryRecord>();
        var days = (request.End.Date - request.Start.Date).Days;

        for (var i = 0; i <= days; i++)
        {
            var date = request.Start.Date.AddDays(i);
            var response = await dynamoDb.QueryAsync(new QueryRequest
            {
                TableName = config.TableName,
                KeyConditionExpression = "PK = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pk", new AttributeValue { S = MarketDataStorageContract.BuildInventoryPartitionKey(date) } }
                }
            });

            records.AddRange(response.Items.Select(MapInventory));
        }

        return records
            .Where(record => request.Timespan is null || record.Timespan == request.Timespan)
            .Where(record => request.Multiplier is null || record.Multiplier == request.Multiplier)
            .OrderBy(record => record.Date)
            .ThenBy(record => record.Timespan)
            .ToList();
    }

    public async Task<MarketDataRunRecord> PutRunRecord(MarketDataRunRecord record)
    {
        var response = await dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = config.TableName,
            Item = MapRun(record)
        });

        if (response.HttpStatusCode != HttpStatusCode.OK)
        {
            logger.LogError("Failed to put market data run record {RunId}.", record.RunId);
        }

        return record;
    }

    public async Task<List<MarketDataRunRecord>> ListRuns(int limit = 50)
    {
        var response = await dynamoDb.QueryAsync(new QueryRequest
        {
            TableName = config.TableName,
            KeyConditionExpression = "PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = MarketDataStorageContract.BuildRunPartitionKey() } }
            },
            Limit = limit
        });

        return response.Items
            .Select(MapRun)
            .OrderByDescending(record => record.StartedAt)
            .ToList();
    }

    private static Dictionary<string, AttributeValue> MapInventory(MarketDataInventoryRecord record)
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

        AddOptional(item, "ObjectSize", record.ObjectSize);
        AddOptional(item, "ETag", record.ETag);
        AddOptional(item, "RecordCount", record.RecordCount);
        AddOptional(item, "StartedAt", record.StartedAt);
        AddOptional(item, "CompletedAt", record.CompletedAt);
        AddOptional(item, "RunId", record.RunId);
        AddOptional(item, "Error", record.Error);

        return item;
    }

    private static MarketDataInventoryRecord MapInventory(Dictionary<string, AttributeValue> item)
    {
        return new MarketDataInventoryRecord
        {
            Date = DateTimeOffset.Parse(item["Date"].S),
            Multiplier = int.Parse(item["Multiplier"].N),
            Timespan = Enum.Parse<Timespan>(item["Timespan"].S),
            Bucket = GetString(item, "Bucket") ?? string.Empty,
            Key = GetString(item, "Key") ?? string.Empty,
            Status = Enum.Parse<MarketDataStatus>(item["Status"].S),
            ObjectSize = GetLong(item, "ObjectSize"),
            ETag = GetString(item, "ETag"),
            RecordCount = GetInt(item, "RecordCount"),
            StartedAt = GetDate(item, "StartedAt"),
            CompletedAt = GetDate(item, "CompletedAt"),
            Source = GetString(item, "Source") ?? string.Empty,
            RunId = GetString(item, "RunId"),
            Error = GetString(item, "Error")
        };
    }

    private static Dictionary<string, AttributeValue> MapRun(MarketDataRunRecord record)
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

        return item;
    }

    private static MarketDataRunRecord MapRun(Dictionary<string, AttributeValue> item)
    {
        return new MarketDataRunRecord
        {
            RunId = item["RunId"].S,
            Start = DateTimeOffset.Parse(item["Start"].S),
            End = DateTimeOffset.Parse(item["End"].S),
            Timespans = JsonSerializer.Deserialize<List<Timespan>>(item["Timespans"].S) ?? [],
            Multiplier = int.Parse(item["Multiplier"].N),
            Status = Enum.Parse<MarketDataStatus>(item["Status"].S),
            Source = GetString(item, "Source") ?? string.Empty,
            RequestedCount = GetInt(item, "RequestedCount") ?? 0,
            CompletedCount = GetInt(item, "CompletedCount") ?? 0,
            FailedCount = GetInt(item, "FailedCount") ?? 0,
            StartedAt = GetDate(item, "StartedAt") ?? DateTimeOffset.MinValue,
            CompletedAt = GetDate(item, "CompletedAt"),
            Error = GetString(item, "Error")
        };
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

    private static string? GetString(Dictionary<string, AttributeValue> item, string name)
    {
        return item.TryGetValue(name, out var value) ? value.S : null;
    }

    private static long? GetLong(Dictionary<string, AttributeValue> item, string name)
    {
        return item.TryGetValue(name, out var value) && long.TryParse(value.N, out var number) ? number : null;
    }

    private static int? GetInt(Dictionary<string, AttributeValue> item, string name)
    {
        return item.TryGetValue(name, out var value) && int.TryParse(value.N, out var number) ? number : null;
    }

    private static DateTimeOffset? GetDate(Dictionary<string, AttributeValue> item, string name)
    {
        return item.TryGetValue(name, out var value) && DateTimeOffset.TryParse(value.S, out var date) ? date : null;
    }
}
