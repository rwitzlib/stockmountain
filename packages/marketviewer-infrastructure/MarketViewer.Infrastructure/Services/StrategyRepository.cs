using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Models.Strategy;
using MarketViewer.Contracts.Records.Strategy;
using MarketViewer.Core.Services;
using MarketViewer.Infrastructure.Config;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace MarketViewer.Infrastructure.Services;

public class StrategyRepository(StrategyConfig config, IAmazonDynamoDB dynamoDb, ILogger<StrategyRepository> logger) : IStrategyRepository
{
    private const int EXPIRY = 30;

    public async Task<StrategyDto> Create(StrategyDto strategy)
    {
        try
        {
            var request = new PutItemRequest
            {
                TableName = config.TableName,
                Item = MapToDynamoDbItem(strategy)
            };

            var response = await dynamoDb.PutItemAsync(request);

            logger.LogInformation("Put strategy with ID {Id}, response status: {StatusCode}", strategy.Id, response.HttpStatusCode);

            await CreateOrUpdateActiveStrategies(strategy, 1);

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
                    { "PK", new AttributeValue { S = $"BOT#{id}" } },
                    { "SK", new AttributeValue { S = "CONFIG" } }
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
            return response.Items.Select(MapToStrategyDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting all strategies");
            return [];
        }
    }

    public async Task<IEnumerable<StrategyDto>> ListByVisibility(VisibilityType visibility)
    {
        try
        {
            var request = new QueryRequest
            {
                TableName = config.TableName,
                IndexName = config.VisibilityIndexName,
                KeyConditionExpression = "Visibility = :visibility",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":visibility", new AttributeValue { S = visibility.ToString() } }
                },
                ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
            };

            var response = await dynamoDb.QueryAsync(request);
            return response.Items.Select(MapToStrategyDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting public strategies");
            return [];
        }
    }

    public async Task<IEnumerable<StrategyEntrySettings>> ListUniqueActiveStrategies()
    {
        try
        {
            var request = new QueryRequest
            {
                TableName = config.TableName,
                KeyConditionExpression = "PK = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pk", new AttributeValue { S = "ACTIVE_STRATEGIES" } },
                }
            };

            var response = await dynamoDb.QueryAsync(request);

            if (response.Items.Count == 0)
            {
                return [];
            }

            var strategies = response.Items.Select(q => JsonSerializer.Deserialize<StrategyEntrySettings>(Document.FromAttributeMap(q["EntrySettings"].M).ToJson())).ToList();

            return strategies;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing strategy hashes");
            return [];
        }
    }

    public async Task<StrategyDto> Update(StrategyDto strategy, StrategyDto oldStrategy)
    {
        try
        {
            var request = new PutItemRequest
            {
                TableName = config.TableName,
                Item = MapToDynamoDbItem(strategy)
            };

            var newStrategyHash = strategy.ComputeStrategyHash();
            var oldStrategyHash = oldStrategy.ComputeStrategyHash();

            var response = await dynamoDb.PutItemAsync(request);

            if (!string.Equals(newStrategyHash, oldStrategyHash))
            {
                logger.LogInformation("Strategy hash changed from {OldHash} to {NewHash} for strategy ID {Id}", oldStrategyHash, newStrategyHash, strategy.Id);
                await CreateOrUpdateActiveStrategies(oldStrategy, -1);
                await CreateOrUpdateActiveStrategies(strategy, 1);
            }

            logger.LogInformation("Put strategy with ID {Id}, response status: {StatusCode}", strategy.Id, response.HttpStatusCode);

            return strategy;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error putting strategy with ID {Id}", strategy.Id);
            return null;
        }
    }

    public async Task<bool> Delete(StrategyDto strategy)
    {
        try
        {
            var request = new UpdateItemRequest
            {
                TableName = config.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S =  $"BOT#{strategy.Id}" } },
                    { "SK", new AttributeValue { S = "CONFIG" } }
                },
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#TTL", "Expiry" }
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":time", new AttributeValue { N = DateTimeOffset.Now.AddDays(EXPIRY).ToUnixTimeSeconds().ToString() } }
                },
                UpdateExpression = "SET #TTL = :time",
                ReturnValues = "UPDATED_NEW"
            };

            var response = await dynamoDb.UpdateItemAsync(request);

            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                logger.LogError("Failed to delete strategy with ID {Id}", strategy.Id);
                return false;
            }

            await CreateOrUpdateActiveStrategies(strategy, -1);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting strategy with ID {Id}", strategy.Id);
            return false;
        }
    }

    #region Strategy State & Balance History

    public async Task<StrategyStateRecord> GetState(string strategyId)
    {
        try
        {
            var request = new GetItemRequest
            {
                TableName = config.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"BOT#{strategyId}" } },
                    { "SK", new AttributeValue { S = "STATE" } }
                }
            };

            var response = await dynamoDb.GetItemAsync(request);

            if (response.Item == null || response.Item.Count == 0)
            {
                logger.LogDebug("No state found for strategy {StrategyId}", strategyId);
                return null;
            }

            return MapToStrategyStateRecord(response.Item);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting state for strategy {StrategyId}", strategyId);
            return null;
        }
    }

    public async Task<IEnumerable<BalanceHistoryRecord>> GetBalanceHistory(string strategyId, string startDate, string endDate)
    {
        try
        {
            var request = new QueryRequest
            {
                TableName = config.TableName,
                KeyConditionExpression = "PK = :pk AND SK BETWEEN :start AND :end",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pk", new AttributeValue { S = $"BOT#{strategyId}" } },
                    { ":start", new AttributeValue { S = $"BALANCE#{startDate}" } },
                    { ":end", new AttributeValue { S = $"BALANCE#{endDate}" } }
                },
                ScanIndexForward = true // Oldest first
            };

            var response = await dynamoDb.QueryAsync(request);

            if (response.Items == null || response.Items.Count == 0)
            {
                return [];
            }

            return response.Items.Select(MapToBalanceHistoryRecord);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting balance history for strategy {StrategyId}", strategyId);
            return [];
        }
    }

    #endregion

    #region Private Methods

    private static StrategyStateRecord MapToStrategyStateRecord(Dictionary<string, AttributeValue> item)
    {
        var state = new StrategyStateRecord
        {
            StrategyId = item.TryGetValue("StrategyId", out var strategyId) ? strategyId.S : null,
            CashBalance = item.TryGetValue("CashBalance", out var cash) ? decimal.Parse(cash.N) : 0,
            TotalEntryCost = item.TryGetValue("TotalEntryCost", out var entryCost) ? decimal.Parse(entryCost.N) : 0,
            UnrealizedPnl = item.TryGetValue("UnrealizedPnl", out var pnl) ? decimal.Parse(pnl.N) : 0,
            OpenPositionsCount = item.TryGetValue("OpenPositionsCount", out var count) ? int.Parse(count.N) : 0,
            LastTradeAt = item.TryGetValue("LastTradeAt", out var lastTrade) ? long.Parse(lastTrade.N) : 0,
            Version = item.TryGetValue("Version", out var version) ? long.Parse(version.N) : 0
        };

        // Parse OpenTickers (filter out placeholder)
        if (item.TryGetValue("OpenTickers", out var tickers) && tickers.SS != null)
        {
            state.OpenTickers = tickers.SS
                .Where(t => t != "__EMPTY__")
                .ToHashSet();
        }

        // Parse Cooldowns map
        if (item.TryGetValue("Cooldowns", out var cooldowns) && cooldowns.M != null)
        {
            state.Cooldowns = cooldowns.M
                .ToDictionary(kv => kv.Key, kv => long.Parse(kv.Value.N));
        }

        return state;
    }

    private static BalanceHistoryRecord MapToBalanceHistoryRecord(Dictionary<string, AttributeValue> item)
    {
        return new BalanceHistoryRecord
        {
            StrategyId = item.TryGetValue("StrategyId", out var strategyId) ? strategyId.S : null,
            Date = item.TryGetValue("Date", out var date) ? date.S : null,
            CashBalance = item.TryGetValue("CashBalance", out var cash) ? decimal.Parse(cash.N) : 0,
            TotalEntryCost = item.TryGetValue("TotalEntryCost", out var entryCost) ? decimal.Parse(entryCost.N) : 0,
            UnrealizedPnl = item.TryGetValue("UnrealizedPnl", out var pnl) ? decimal.Parse(pnl.N) : 0,
            PositionValue = item.TryGetValue("PositionValue", out var posValue) ? decimal.Parse(posValue.N) : 0,
            CurrentBalance = item.TryGetValue("CurrentBalance", out var balance) ? decimal.Parse(balance.N) : 0,
            OpenPositionsCount = item.TryGetValue("OpenPositionsCount", out var count) ? int.Parse(count.N) : 0,
            RecordedAt = item.TryGetValue("RecordedAt", out var recordedAt) ? long.Parse(recordedAt.N) : 0,
            SnapshotType = item.TryGetValue("SnapshotType", out var snapshotType) ? snapshotType.S : null
        };
    }

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
            EntrySettings = JsonSerializer.Deserialize<StrategyEntrySettings>(Document.FromAttributeMap(item["EntrySettings"].M).ToJson()),
            Expiry = item.TryGetValue("Expiry", out AttributeValue value) ? int.Parse(value.N) : null
        };
    }

    private static Dictionary<string, AttributeValue> MapToDynamoDbItem(StrategyDto strategy)
    {
        return new Dictionary<string, AttributeValue>
        {
            { "PK", new AttributeValue { S = $"BOT#{strategy.Id}" } },
            { "SK", new AttributeValue { S = $"CONFIG" } },
            { "UserId", new AttributeValue { S = strategy.UserId } },
            { "Name", new AttributeValue { S = strategy.Name } },
            { "#State", new AttributeValue { S = strategy.State.ToString() } },
            { "Type", new AttributeValue { S = strategy.Type.ToString() } },
            { "Visibility", new AttributeValue { S = strategy.Visibility.ToString() } },
            { "Integration", new AttributeValue { S = strategy.Integration.ToString() } },
            { "PositionSettings", new AttributeValue { M = Document.FromJson(JsonSerializer.Serialize(strategy.PositionSettings)).ToAttributeMap()} },
            { "ExitSettings", new AttributeValue { M = Document.FromJson(JsonSerializer.Serialize(strategy.ExitSettings)).ToAttributeMap() } },
            { "EntrySettings", new AttributeValue { M = Document.FromJson(JsonSerializer.Serialize(strategy.EntrySettings)).ToAttributeMap() } },
            { "StrategyHash", new AttributeValue { S = strategy.ComputeStrategyHash() } }
        };
    }

    private async Task CreateOrUpdateActiveStrategies(StrategyDto strategy, int increment)
    {
        var strategyHash = strategy.ComputeStrategyHash();

        try
        {
            if (increment > 0)
            {
                // Use atomic UpdateItem with if_not_exists to avoid TOCTOU race condition
                // This atomically creates the item with Count=increment if it doesn't exist,
                // or increments the existing Count if it does
                var updateRequest = new UpdateItemRequest
                {
                    TableName = config.TableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        { "PK", new AttributeValue { S = "ACTIVE_STRATEGIES" } },
                        { "SK", new AttributeValue { S = $"STRAT#{strategyHash}"} }
                    },
                    ExpressionAttributeNames = new Dictionary<string, string>
                    {
                        { "#C", "Count" },
                        { "#E", "EntrySettings" }
                    },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":inc", new AttributeValue { N = increment.ToString() } },
                        { ":zero", new AttributeValue { N = "0" } },
                        { ":entrySettings", new AttributeValue { M = Document.FromJson(JsonSerializer.Serialize(strategy.EntrySettings)).ToAttributeMap() } }
                    },
                    UpdateExpression = "SET #C = if_not_exists(#C, :zero) + :inc, #E = if_not_exists(#E, :entrySettings)",
                    ReturnValues = "UPDATED_NEW"
                };

                var response = await dynamoDb.UpdateItemAsync(updateRequest);
                logger.LogInformation("Incremented ACTIVE_STRATEGIES for strategy hash {StrategyHash}, new count: {Count}", strategyHash, response.Attributes["Count"].N);
            }
            else if (increment < 0)
            {
                // Use conditional update to only decrement if the item exists
                // This prevents creating a record with negative count for non-existent items
                var updateRequest = new UpdateItemRequest
                {
                    TableName = config.TableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        { "PK", new AttributeValue { S = "ACTIVE_STRATEGIES" } },
                        { "SK", new AttributeValue { S = $"STRAT#{strategyHash}"} }
                    },
                    ExpressionAttributeNames = new Dictionary<string, string>
                    {
                        { "#C", "Count" }
                    },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":inc", new AttributeValue { N = increment.ToString() } }
                    },
                    UpdateExpression = "SET #C = #C + :inc",
                    ConditionExpression = "attribute_exists(PK)",
                    ReturnValues = "UPDATED_NEW"
                };

                try
                {
                    var response = await dynamoDb.UpdateItemAsync(updateRequest);
                    var count = response.Attributes["Count"].N;

                    logger.LogInformation("Decremented ACTIVE_STRATEGIES for strategy hash {StrategyHash}, new count: {Count}", strategyHash, count);

                    if (count == "0")
                    {
                        var deleteRequest = new DeleteItemRequest
                        {
                            TableName = config.TableName,
                            Key = new Dictionary<string, AttributeValue>
                            {
                                { "PK", new AttributeValue { S = "ACTIVE_STRATEGIES" } },
                                { "SK", new AttributeValue { S = $"STRAT#{strategyHash}"} }
                            }
                        };
                        var deleteResponse = await dynamoDb.DeleteItemAsync(deleteRequest);
                        logger.LogInformation("Deleted ACTIVE_STRATEGIES for strategy hash {StrategyHash}, response status: {StatusCode}", strategyHash, deleteResponse.HttpStatusCode);
                    }
                }
                catch (ConditionalCheckFailedException)
                {
                    // Item doesn't exist, nothing to decrement - this is expected behavior
                    logger.LogDebug("ACTIVE_STRATEGIES record for strategy hash {StrategyHash} does not exist, skipping decrement", strategyHash);
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating or updating ACTIVE_STRATEGIES for strategy hash {StrategyHash}", strategyHash);
        }
    }

    #endregion
}
