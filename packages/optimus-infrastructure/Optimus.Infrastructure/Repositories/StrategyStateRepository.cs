using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MarketViewer.Contracts.Records.Strategy;
using Microsoft.Extensions.Logging;
using Optimus.Infrastructure.Config;

namespace Optimus.Infrastructure.Repositories;

public class StrategyStateRepository(
    StrategyConfig config,
    IAmazonDynamoDB dynamoDb,
    ILogger<StrategyStateRepository> logger)
{
    private const string StateSkValue = "STATE";

    /// <summary>
    /// Fetches the current state for a strategy. Returns null if not found.
    /// </summary>
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
                    { "SK", new AttributeValue { S = StateSkValue } }
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
            logger.LogError(ex, "Error fetching state for strategy {StrategyId}", strategyId);
            throw;
        }
    }

    /// <summary>
    /// Gets existing state or creates initial state if it doesn't exist.
    /// This provides lazy initialization for strategies created before state tracking was added.
    /// </summary>
    public async Task<StrategyStateRecord> GetOrCreateState(string strategyId, decimal startingBalance)
    {
        var state = await GetState(strategyId);
        if (state != null)
        {
            return state;
        }

        // State doesn't exist, create it
        var created = await CreateState(strategyId, startingBalance);
        if (created)
        {
            // Fetch the newly created state
            return await GetState(strategyId);
        }

        // Race condition - another process created it, fetch again
        return await GetState(strategyId);
    }

    /// <summary>
    /// Creates initial state for a strategy. Uses conditional write to prevent overwrites.
    /// Returns true if created, false if already exists.
    /// </summary>
    public async Task<bool> CreateState(string strategyId, decimal startingBalance)
    {
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var request = new PutItemRequest
            {
                TableName = config.TableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"BOT#{strategyId}" } },
                    { "SK", new AttributeValue { S = StateSkValue } },
                    { "StrategyId", new AttributeValue { S = strategyId } },
                    { "CashBalance", new AttributeValue { N = startingBalance.ToString() } },
                    { "TotalEntryCost", new AttributeValue { N = "0" } },
                    { "UnrealizedPnl", new AttributeValue { N = "0" } },
                    { "OpenPositionsCount", new AttributeValue { N = "0" } },
                    { "OpenTickers", new AttributeValue { SS = ["__EMPTY__"] } }, // DynamoDB doesn't allow empty sets
                    { "Cooldowns", new AttributeValue { M = new Dictionary<string, AttributeValue>() } },
                    { "LastTradeAt", new AttributeValue { N = now.ToString() } },
                    { "Version", new AttributeValue { N = "1" } }
                },
                ConditionExpression = "attribute_not_exists(PK)"
            };

            await dynamoDb.PutItemAsync(request);
            logger.LogInformation("Created initial state for strategy {StrategyId} with balance {Balance}",
                strategyId, startingBalance);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            logger.LogDebug("State already exists for strategy {StrategyId}", strategyId);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating state for strategy {StrategyId}", strategyId);
            throw;
        }
    }

    /// <summary>
    /// Atomically reserves a position slot before executing a trade.
    /// This combines eligibility checks with state mutation in a single atomic operation.
    /// Checks: ticker not in OpenTickers (if !allowSimultaneous), max positions not exceeded, sufficient cash.
    /// If all checks pass: decrements cash, adds ticker to open set, increments position count.
    /// Returns a result indicating success or the reason for failure.
    /// </summary>
    public async Task<ReservePositionResult> TryReservePosition(
        string strategyId,
        string ticker,
        decimal positionCost,
        int maxConcurrentPositions,
        bool allowSimultaneous)
    {
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Build condition expression based on allowSimultaneous setting
            // We need to check:
            // 1. CashBalance >= cost
            // 2. OpenPositionsCount < maxConcurrentPositions
            // 3. If !allowSimultaneous: ticker not in OpenTickers
            var conditionParts = new List<string>
            {
                "CashBalance >= :cost",
                "OpenPositionsCount < :maxPositions"
            };

            var expressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":cost", new AttributeValue { N = positionCost.ToString() } },
                { ":one", new AttributeValue { N = "1" } },
                { ":zero", new AttributeValue { N = "0" } },
                { ":now", new AttributeValue { N = now.ToString() } },
                { ":maxPositions", new AttributeValue { N = maxConcurrentPositions.ToString() } },
                { ":ticker", new AttributeValue { SS = [ticker] } }
            };

            if (!allowSimultaneous)
            {
                // Check that ticker is NOT in OpenTickers
                conditionParts.Add("NOT contains(OpenTickers, :tickerStr)");
                expressionAttributeValues[":tickerStr"] = new AttributeValue { S = ticker };
            }

            var request = new UpdateItemRequest
            {
                TableName = config.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"BOT#{strategyId}" } },
                    { "SK", new AttributeValue { S = StateSkValue } }
                },
                UpdateExpression = @"
                    SET CashBalance = CashBalance - :cost,
                        TotalEntryCost = if_not_exists(TotalEntryCost, :zero) + :cost,
                        OpenPositionsCount = OpenPositionsCount + :one,
                        LastTradeAt = :now,
                        Version = Version + :one
                    ADD OpenTickers :ticker",
                ConditionExpression = string.Join(" AND ", conditionParts),
                ExpressionAttributeValues = expressionAttributeValues,
                ReturnValues = ReturnValue.ALL_NEW
            };

            var response = await dynamoDb.UpdateItemAsync(request);
            var newVersion = long.Parse(response.Attributes["Version"].N);

            logger.LogInformation(
                "Reserved position for strategy {StrategyId}, ticker {Ticker}, cost {Cost}",
                strategyId, ticker, positionCost);

            return ReservePositionResult.Success(newVersion);
        }
        catch (ConditionalCheckFailedException)
        {
            logger.LogWarning(
                "Position reservation failed for strategy {StrategyId}, ticker {Ticker} - eligibility check failed",
                strategyId, ticker);
            return ReservePositionResult.Failed("Eligibility check failed: insufficient funds, max positions reached, or position already open");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reserving position for strategy {StrategyId}, ticker {Ticker}", strategyId, ticker);
            throw;
        }
    }

    /// <summary>
    /// Rolls back a position reservation if the trade execution fails.
    /// Restores cash, removes ticker from open set, decrements position count.
    /// </summary>
    public async Task<bool> RollbackPositionReservation(
        string strategyId,
        string ticker,
        decimal positionCost,
        long expectedVersion)
    {
        try
        {
            var request = new UpdateItemRequest
            {
                TableName = config.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"BOT#{strategyId}" } },
                    { "SK", new AttributeValue { S = StateSkValue } }
                },
                UpdateExpression = @"
                    SET CashBalance = CashBalance + :cost,
                        TotalEntryCost = TotalEntryCost - :cost,
                        OpenPositionsCount = OpenPositionsCount - :one,
                        Version = Version + :one
                    DELETE OpenTickers :ticker",
                ConditionExpression = "Version = :expectedVersion",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":cost", new AttributeValue { N = positionCost.ToString() } },
                    { ":one", new AttributeValue { N = "1" } },
                    { ":expectedVersion", new AttributeValue { N = expectedVersion.ToString() } },
                    { ":ticker", new AttributeValue { SS = [ticker] } }
                }
            };

            await dynamoDb.UpdateItemAsync(request);
            logger.LogInformation(
                "Rolled back position reservation for strategy {StrategyId}, ticker {Ticker}",
                strategyId, ticker);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            logger.LogWarning(
                "Rollback failed for strategy {StrategyId}, ticker {Ticker} - version mismatch. Manual reconciliation may be needed.",
                strategyId, ticker);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error rolling back position for strategy {StrategyId}, ticker {Ticker}", strategyId, ticker);
            throw;
        }
    }

    /// <summary>
    /// Adjusts the cash balance after a trade is executed when the actual cost differs from the reserved amount.
    /// This is called after a successful buy when shares * price != reserved position size.
    /// </summary>
    /// <param name="strategyId">The strategy ID</param>
    /// <param name="reservedAmount">The amount that was reserved (typically position.Size)</param>
    /// <param name="actualAmount">The actual cost of the trade (shares * price)</param>
    /// <returns>True if adjustment succeeded, false on version mismatch</returns>
    public async Task<bool> AdjustPositionCost(
        string strategyId,
        decimal reservedAmount,
        decimal actualAmount)
    {
        // Calculate the difference: if actual < reserved, we refund; if actual > reserved, we debit more
        var difference = reservedAmount - actualAmount;

        if (difference == 0)
        {
            // No adjustment needed
            return true;
        }

        try
        {
            // When adjusting cost:
            // - If actual < reserved: refund cash, reduce TotalEntryCost
            // - If actual > reserved: debit more cash, increase TotalEntryCost
            // The difference variable = reserved - actual
            // So: CashBalance += difference, TotalEntryCost -= difference
            var request = new UpdateItemRequest
            {
                TableName = config.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"BOT#{strategyId}" } },
                    { "SK", new AttributeValue { S = StateSkValue } }
                },
                UpdateExpression = "SET CashBalance = CashBalance + :difference, TotalEntryCost = TotalEntryCost - :difference, Version = Version + :one",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":difference", new AttributeValue { N = difference.ToString() } },
                    { ":one", new AttributeValue { N = "1" } }
                }
            };

            await dynamoDb.UpdateItemAsync(request);

            if (difference > 0)
            {
                logger.LogDebug(
                    "Refunded {Difference} to strategy {StrategyId} (reserved: {Reserved}, actual: {Actual})",
                    difference, strategyId, reservedAmount, actualAmount);
            }
            else
            {
                logger.LogDebug(
                    "Debited additional {Difference} from strategy {StrategyId} (reserved: {Reserved}, actual: {Actual})",
                    -difference, strategyId, reservedAmount, actualAmount);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error adjusting position cost for strategy {StrategyId}. Reserved: {Reserved}, Actual: {Actual}",
                strategyId, reservedAmount, actualAmount);
            throw;
        }
    }

    /// <summary>
    /// Atomically updates state when closing a position.
    /// Increments cash with close value, removes ticker from open set, decrements position count, 
    /// decreases total entry cost, and sets cooldown.
    /// </summary>
    public async Task<bool> UpdateStateOnClose(
        string strategyId,
        string ticker,
        decimal closeValue,
        decimal entryCost,
        long cooldownExpirySeconds,
        long expectedVersion)
    {
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var request = new UpdateItemRequest
            {
                TableName = config.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"BOT#{strategyId}" } },
                    { "SK", new AttributeValue { S = StateSkValue } }
                },
                UpdateExpression = @"
                    SET CashBalance = CashBalance + :closeValue,
                        TotalEntryCost = TotalEntryCost - :entryCost,
                        OpenPositionsCount = OpenPositionsCount - :one,
                        LastTradeAt = :now,
                        Version = Version + :one,
                        Cooldowns.#ticker = :cooldownExpiry
                    DELETE OpenTickers :ticker",
                ConditionExpression = "Version = :expectedVersion",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#ticker", ticker }
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":closeValue", new AttributeValue { N = closeValue.ToString() } },
                    { ":entryCost", new AttributeValue { N = entryCost.ToString() } },
                    { ":one", new AttributeValue { N = "1" } },
                    { ":now", new AttributeValue { N = now.ToString() } },
                    { ":expectedVersion", new AttributeValue { N = expectedVersion.ToString() } },
                    { ":ticker", new AttributeValue { SS = [ticker] } },
                    { ":cooldownExpiry", new AttributeValue { N = cooldownExpirySeconds.ToString() } }
                }
            };

            await dynamoDb.UpdateItemAsync(request);
            logger.LogInformation(
                "Updated state on close for strategy {StrategyId}, ticker {Ticker}, closeValue {CloseValue}, entryCost {EntryCost}",
                strategyId, ticker, closeValue, entryCost);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            logger.LogWarning("State update on close failed for strategy {StrategyId} - version mismatch", strategyId);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating state on close for strategy {StrategyId}", strategyId);
            throw;
        }
    }

    /// <summary>
    /// Updates the unrealized P/L for a strategy.
    /// </summary>
    public async Task<bool> UpdateUnrealizedPnl(string strategyId, decimal unrealizedPnl, long expectedVersion)
    {
        try
        {
            var request = new UpdateItemRequest
            {
                TableName = config.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"BOT#{strategyId}" } },
                    { "SK", new AttributeValue { S = StateSkValue } }
                },
                UpdateExpression = "SET UnrealizedPnl = :pnl, Version = Version + :one",
                ConditionExpression = "Version = :expectedVersion",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pnl", new AttributeValue { N = unrealizedPnl.ToString() } },
                    { ":one", new AttributeValue { N = "1" } },
                    { ":expectedVersion", new AttributeValue { N = expectedVersion.ToString() } }
                }
            };

            await dynamoDb.UpdateItemAsync(request);
            logger.LogDebug("Updated unrealized P/L for strategy {StrategyId} to {Pnl}", strategyId, unrealizedPnl);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            logger.LogWarning("Unrealized P/L update failed for strategy {StrategyId} - version mismatch", strategyId);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating unrealized P/L for strategy {StrategyId}", strategyId);
            throw;
        }
    }

    /// <summary>
    /// Cleans up expired cooldowns from the state.
    /// </summary>
    public async Task CleanupExpiredCooldowns(string strategyId, IEnumerable<string> expiredTickers)
    {
        try
        {
            var tickerList = expiredTickers.ToList();
            if (tickerList.Count == 0) return;

            // Build REMOVE expression for each expired ticker
            var removeExpressions = tickerList.Select((t, i) => $"Cooldowns.#t{i}");
            var expressionAttributeNames = tickerList
                .Select((t, i) => new { Key = $"#t{i}", Value = t })
                .ToDictionary(x => x.Key, x => x.Value);

            var request = new UpdateItemRequest
            {
                TableName = config.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"BOT#{strategyId}" } },
                    { "SK", new AttributeValue { S = StateSkValue } }
                },
                UpdateExpression = $"REMOVE {string.Join(", ", removeExpressions)}",
                ExpressionAttributeNames = expressionAttributeNames
            };

            await dynamoDb.UpdateItemAsync(request);
            logger.LogDebug("Cleaned up {Count} expired cooldowns for strategy {StrategyId}",
                tickerList.Count, strategyId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cleaning up cooldowns for strategy {StrategyId}", strategyId);
            // Don't throw - cooldown cleanup is non-critical
        }
    }

    #region Balance History Methods

    /// <summary>
    /// Writes a balance history snapshot for a strategy.
    /// Uses date as sort key for efficient range queries.
    /// </summary>
    public async Task<bool> WriteBalanceHistoryRecord(BalanceHistoryRecord history)
    {
        try
        {
            var request = new PutItemRequest
            {
                TableName = config.TableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"BOT#{history.StrategyId}" } },
                    { "SK", new AttributeValue { S = $"BALANCE#{history.Date}" } },
                    { "StrategyId", new AttributeValue { S = history.StrategyId } },
                    { "Date", new AttributeValue { S = history.Date } },
                    { "CashBalance", new AttributeValue { N = history.CashBalance.ToString() } },
                    { "TotalEntryCost", new AttributeValue { N = history.TotalEntryCost.ToString() } },
                    { "UnrealizedPnl", new AttributeValue { N = history.UnrealizedPnl.ToString() } },
                    { "PositionValue", new AttributeValue { N = history.PositionValue.ToString() } },
                    { "CurrentBalance", new AttributeValue { N = history.CurrentBalance.ToString() } },
                    { "OpenPositionsCount", new AttributeValue { N = history.OpenPositionsCount.ToString() } },
                    { "RecordedAt", new AttributeValue { N = history.RecordedAt.ToString() } },
                    { "SnapshotType", new AttributeValue { S = history.SnapshotType ?? "close" } }
                }
            };

            await dynamoDb.PutItemAsync(request);
            logger.LogDebug(
                "Wrote balance history for strategy {StrategyId}, date {Date}, balance {Balance}",
                history.StrategyId, history.Date, history.CurrentBalance);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error writing balance history for strategy {StrategyId}", history.StrategyId);
            return false;
        }
    }

    /// <summary>
    /// Queries balance history for a strategy within a date range.
    /// </summary>
    /// <param name="strategyId">The strategy ID</param>
    /// <param name="startDate">Start date (inclusive, YYYY-MM-DD format)</param>
    /// <param name="endDate">End date (inclusive, YYYY-MM-DD format)</param>
    /// <returns>List of balance history entries ordered by date</returns>
    public async Task<IEnumerable<BalanceHistoryRecord>> GetBalanceHistoryRecord(
        string strategyId,
        string startDate,
        string endDate)
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
            logger.LogError(ex,
                "Error querying balance history for strategy {StrategyId} from {StartDate} to {EndDate}",
                strategyId, startDate, endDate);
            throw;
        }
    }

    /// <summary>
    /// Gets the most recent balance history entry for a strategy.
    /// </summary>
    public async Task<BalanceHistoryRecord> GetLatestBalanceHistoryRecord(string strategyId)
    {
        try
        {
            var request = new QueryRequest
            {
                TableName = config.TableName,
                KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pk", new AttributeValue { S = $"BOT#{strategyId}" } },
                    { ":prefix", new AttributeValue { S = "BALANCE#" } }
                },
                ScanIndexForward = false, // Most recent first
                Limit = 1
            };

            var response = await dynamoDb.QueryAsync(request);

            if (response.Items == null || response.Items.Count == 0)
            {
                return null;
            }

            return MapToBalanceHistoryRecord(response.Items[0]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting latest balance history for strategy {StrategyId}", strategyId);
            throw;
        }
    }

    /// <summary>
    /// Gets the balance history entry for a specific date.
    /// </summary>
    public async Task<BalanceHistoryRecord> GetBalanceHistoryRecordForDate(string strategyId, string date)
    {
        try
        {
            var request = new GetItemRequest
            {
                TableName = config.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"BOT#{strategyId}" } },
                    { "SK", new AttributeValue { S = $"BALANCE#{date}" } }
                }
            };

            var response = await dynamoDb.GetItemAsync(request);

            if (response.Item == null || response.Item.Count == 0)
            {
                return null;
            }

            return MapToBalanceHistoryRecord(response.Item);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error getting balance history for strategy {StrategyId} on date {Date}",
                strategyId, date);
            throw;
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

    #endregion
}

/// <summary>
/// Result of attempting to reserve a position slot.
/// </summary>
public class ReservePositionResult
{
    public bool IsSuccess { get; private set; }
    public string FailureReason { get; private set; }
    public long NewVersion { get; private set; }

    private ReservePositionResult() { }

    public static ReservePositionResult Success(long newVersion) => new()
    {
        IsSuccess = true,
        NewVersion = newVersion
    };

    public static ReservePositionResult Failed(string reason) => new()
    {
        IsSuccess = false,
        FailureReason = reason
    };
}

