using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Records;
using Microsoft.Extensions.Logging;
using Optimus.Infrastructure.Config;
using System.Net;
using System.Text.Json;

namespace Optimus.Infrastructure.Repositories;

public class TradeRepository(TradeConfig config, IAmazonDynamoDB dynamodb, ILogger<TradeRepository> logger)
{
    public async Task<bool> Put(TradeRecord order)
    {
        try
        {
            var putItemResponse = await dynamodb.PutItemAsync(new PutItemRequest
            {
                TableName = config.TableName,
                Item = Document.FromJson(JsonSerializer.Serialize(order)).ToAttributeMap()
            });

            if (putItemResponse == null || putItemResponse.HttpStatusCode != HttpStatusCode.OK)
            {
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            logger.LogError("Exception: {message}", e.Message);
            return false;
        }
    }

    /// <summary>
    /// Raises the trade's high-water mark in place. Conditional so it only ever moves up
    /// and never touches a trade that was closed by another writer between the caller's
    /// read and this write (a whole-record Put would resurrect it as open). Returns false
    /// when the condition failed or the write errored; both are safe to ignore.
    /// </summary>
    public async Task<bool> RaiseHighWaterMark(string id, float highWaterMark)
    {
        try
        {
            await dynamodb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = config.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "Id", new AttributeValue { S = id } }
                },
                UpdateExpression = "SET HighWaterMark = :hwm",
                ConditionExpression = "OrderStatus = :open AND (attribute_not_exists(HighWaterMark) OR HighWaterMark < :hwm)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":hwm", new AttributeValue { N = highWaterMark.ToString(System.Globalization.CultureInfo.InvariantCulture) } },
                    { ":open", new AttributeValue { S = TradeStatus.Open.ToString() } }
                }
            });

            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
        catch (Exception e)
        {
            logger.LogError("Exception: {message}", e.Message);
            return false;
        }
    }

    public async Task<TradeRecord> Get(string id)
    {
        try
        {
            var getItemResponse = await dynamodb.GetItemAsync(new GetItemRequest
            {
                TableName = config.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "Id", new AttributeValue { S = id } }
                }
            });

            if (getItemResponse.HttpStatusCode != HttpStatusCode.OK || getItemResponse.Item.Count <= 0)
            {
                return null;
            }

            var record = JsonSerializer.Deserialize<TradeRecord>(Document.FromAttributeMap(getItemResponse.Item).ToJson());
            return record;
        }
        catch (Exception e)
        {
            logger.LogError("Exception: {message}", e.Message);
            return null;
        }
    }

    public async Task<IEnumerable<TradeRecord>> ListTradesByUser(string userId, TradeType? type = null, TradeStatus? status = null)
    {
        try
        {
            var queryRequest = new QueryRequest
            {
                TableName = config.TableName,
                IndexName = config.UserIndexName,
                KeyConditionExpression = "UserId = :userId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    {
                        ":userId",
                        new AttributeValue
                        {
                            S = userId
                        }
                    }
                }
            };

            if (type.HasValue)
            {
                queryRequest.FilterExpression = "Type = :type";
                queryRequest.ExpressionAttributeValues.Add(":type", new AttributeValue { S = type.ToString() });
            }

            if (status is not null)
            {
                queryRequest.FilterExpression = "OrderStatus = :orderStatus";
                queryRequest.ExpressionAttributeValues.Add(":orderStatus", new AttributeValue { S = status.ToString() });
            }

            var queryResponse = await dynamodb.QueryAsync(queryRequest);

            if (queryResponse.HttpStatusCode != HttpStatusCode.OK || queryResponse.Items is not { Count: > 0 })
            {
                return [];
            }

            var records = queryResponse.Items.Select(q => JsonSerializer.Deserialize<TradeRecord>(Document.FromAttributeMap(q).ToJson()));

            return records;
        }
        catch (Exception e)
        {
            logger.LogError("Exception: {message}", e.Message);
            return [];
        }
    }

    public async Task<IEnumerable<TradeRecord>> ListTradesByStrategy(string strategyId, TradeType? type = null, TradeStatus? status = null)
    {
        try
        {
            var queryRequest = new QueryRequest
            {
                TableName = config.TableName,
                IndexName = config.StrategyIndexName,
                KeyConditionExpression = "StrategyId = :strategyId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    {
                        ":strategyId",
                        new AttributeValue
                        {
                            S = strategyId
                        }
                    }
                }
            };

            if (type.HasValue)
            {
                queryRequest.FilterExpression = "Type = :type";
                queryRequest.ExpressionAttributeValues.Add(":type", new AttributeValue { S = type.ToString() });
            }

            if (status.HasValue)
            {
                queryRequest.FilterExpression = "OrderStatus = :orderStatus";
                queryRequest.ExpressionAttributeValues.Add(":orderStatus", new AttributeValue { S = status.ToString() });
            }

            var queryResponse = await dynamodb.QueryAsync(queryRequest);

            if (queryResponse.HttpStatusCode != HttpStatusCode.OK || queryResponse.Items is not { Count: > 0 })
            {
                return [];
            }

            var records = queryResponse.Items.Select(q => JsonSerializer.Deserialize<TradeRecord>(Document.FromAttributeMap(q).ToJson()));

            return records;
        }
        catch (Exception e)
        {
            logger.LogError("Exception: {message}", e.Message);
            return [];
        }
    }
}
