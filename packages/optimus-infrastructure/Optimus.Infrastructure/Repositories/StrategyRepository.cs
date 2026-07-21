using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Models.Strategy;
using Microsoft.Extensions.Logging;
using Optimus.Infrastructure.Config;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Optimus.Infrastructure.Repositories;

public class StrategyRepository(StrategyConfig config, IAmazonDynamoDB dynamoDb, ILogger<StrategyRepository> logger)
{
    public async Task<StrategyDto> Put(StrategyDto strategy)
    {
        try
        {
            var request = new PutItemRequest
            {
                TableName = config.TableName,
                Item = MapToDynamoDbItem(strategy)
            };

            await dynamoDb.PutItemAsync(request);
            return strategy;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error putting strategy with ID {Id}", strategy.Id);
            return null;
        }
    }

    public async Task<StrategyDto> Get(string id)
    {
        try
        {
            var request = new GetItemRequest
            {
                TableName = config.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "Id", new AttributeValue { S = id } }
                }
            };

            var response = await dynamoDb.GetItemAsync(request);
            return response.Item == null ? null : MapToStrategyDto(response.Item);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting strategy with ID {Id}", id);
            return null;
        }
    }

    public async Task<IEnumerable<StrategyDto>> ListByUser(string userId)
    {
        try
        {
            var request = new QueryRequest
            {
                TableName = config.TableName,
                IndexName = config.UserIndexName,
                KeyConditionExpression = "UserId = :userId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":userId", new AttributeValue { S = userId } }
                }
            };

            var response = await dynamoDb.QueryAsync(request);

            var items = (response.Items ?? []).Select(MapToStrategyDto);
            return items;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting all strategies");
            return [];
        }
    }

    public async Task<IEnumerable<StrategyDto>> ListByPublic(bool isPublic)
    {
        try
        {
            var request = new QueryRequest
            {
                TableName = config.TableName,
                IndexName = config.PublicIndexName,
                KeyConditionExpression = "IsPublic = :isPublic",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":isPublic", new AttributeValue { S = isPublic.ToString() } }
                }
            };

            var response = await dynamoDb.QueryAsync(request);
            return (response.Items ?? []).Select(MapToStrategyDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting public strategies");
            return [];
        }
    }

    public async Task<IEnumerable<StrategyDto>> ListByStrategyHash(string strategyHash)
    {
        try
        {
            var request = new QueryRequest
            {
                TableName = config.TableName,
                IndexName = config.StrategyHashIndexName,
                KeyConditionExpression = "StrategyHash = :hash",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":hash", new AttributeValue { S = strategyHash } }
                }
            };

            var response = await dynamoDb.QueryAsync(request);
            return (response.Items ?? []).Select(MapToStrategyDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting strategies by hash {Hash}", strategyHash);
            return [];
        }
    }

    /// <summary>
    /// Lists all active strategies across all users.
    /// Uses a scan with filter - acceptable for periodic batch jobs.
    /// </summary>
    public async Task<IEnumerable<StrategyDto>> ListAll()
    {
        try
        {
            var allStrategies = new List<StrategyDto>();
            Dictionary<string, AttributeValue> lastKey = null;

            do
            {
                var request = new ScanRequest
                {
                    TableName = config.TableName,
                    FilterExpression = "SK = :config AND #state = :active",
                    ExpressionAttributeNames = new Dictionary<string, string>
                    {
                        { "#state", "#State" }
                    },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":config", new AttributeValue { S = "CONFIG" } },
                        { ":active", new AttributeValue { S = StrategyStateType.Active.ToString() } }
                    },
                    ExclusiveStartKey = lastKey
                };

                var response = await dynamoDb.ScanAsync(request);
                allStrategies.AddRange((response.Items ?? []).Select(MapToStrategyDto));
                lastKey = response.LastEvaluatedKey;
            }
            while (lastKey != null && lastKey.Count > 0);

            return allStrategies;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing all strategies");
            return [];
        }
    }

    public async Task<bool> Delete(string id)
    {
        try
        {
            var request = new DeleteItemRequest
            {
                TableName = config.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "Id", new AttributeValue { S = id } }
                }
            };

            var response = await dynamoDb.DeleteItemAsync(request);

            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                logger.LogError("Failed to delete strategy with ID {Id}", id);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting strategy with ID {Id}", id);
            throw; // Re-throw for delete operations since we can't return null
        }
    }

    #region Private Methods

    private static StrategyDto MapToStrategyDto(Dictionary<string, AttributeValue> item)
    {
        return new StrategyDto
        {
            Id = item["PK"].S.Split("BOT#")[1],
            UserId = item["UserId"].S,
            Name = item["Name"].S,
            State = Enum.Parse<StrategyStateType>(item["#State"].S),
            Type = Enum.Parse<TradeType>(item["Type"].S),
            Visibility = Enum.Parse<VisibilityType>(item["Visibility"].S),
            Integration = Enum.Parse<IntegrationType>(item["Integration"].S),
            PositionSettings = JsonSerializer.Deserialize<StrategyPositionSettings>(Document.FromAttributeMap(item["PositionSettings"].M).ToJson()),
            ExitSettings = JsonSerializer.Deserialize<StrategyExitSettings>(Document.FromAttributeMap(item["ExitSettings"].M).ToJson()),
            EntrySettings = JsonSerializer.Deserialize<StrategyEntrySettings>(Document.FromAttributeMap(item["EntrySettings"].M).ToJson())
        };
    }

    private static Dictionary<string, AttributeValue> MapToDynamoDbItem(StrategyDto strategy)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            { "PK", new AttributeValue { S = $"BOT#{strategy.Id}" } },
            { "SK", new AttributeValue { S = "CONFIG" } },
            { "UserId", new AttributeValue { S = strategy.UserId } },
            { "Name", new AttributeValue { S = strategy.Name } },
            { "#State", new AttributeValue { S = strategy.State.ToString() } },
            { "Type", new AttributeValue { S = strategy.Type.ToString() } },
            { "Visibility", new AttributeValue { S = strategy.Visibility.ToString() } },
            { "Integration", new AttributeValue { S = strategy.Integration.ToString() } },
            { "PositionSettings", new AttributeValue { M = Document.FromJson(JsonSerializer.Serialize(strategy.PositionSettings)).ToAttributeMap()} },
            { "ExitSettings", new AttributeValue { M = Document.FromJson(JsonSerializer.Serialize(strategy.ExitSettings)).ToAttributeMap() } },
            { "EntrySettings", new AttributeValue { M = Document.FromJson(JsonSerializer.Serialize(strategy.EntrySettings)).ToAttributeMap() } }
        };

        var strategyHash = ComputeStrategyHash(strategy.EntrySettings);
        if (!string.IsNullOrEmpty(strategyHash))
        {
            item.Add("StrategyHash", new AttributeValue { S = strategyHash });
        }

        return item;
    }

    private static string ComputeStrategyHash(StrategyEntrySettings entrySettings)
    {
        if (entrySettings == null)
        {
            return null;
        }

        var serialized = JsonSerializer.Serialize(entrySettings);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(serialized));
        return Convert.ToBase64String(hashBytes);
    }

    #endregion
}
