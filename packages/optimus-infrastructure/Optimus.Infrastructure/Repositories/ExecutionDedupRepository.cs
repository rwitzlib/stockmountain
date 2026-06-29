using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Optimus.Infrastructure.Config;

namespace Optimus.Infrastructure.Repositories;

public class ExecutionDedupRepository(
    ExecutionDedupConfig config,
    IAmazonDynamoDB dynamoDB,
    ILogger<ExecutionDedupRepository> logger)
{
    /// <summary>
    /// Attempts to record an execution. Returns true if this is the first execution
    /// (record was created), false if already executed (conditional check failed).
    /// </summary>
    public async Task<bool> TryRecordExecution(string strategyId, string ticker, long window)
    {
        var ttl = DateTimeOffset.UtcNow.AddDays(config.TtlDays).ToUnixTimeSeconds();
        var pk = $"STRAT#{strategyId}#TICKER#{ticker}";
        var sk = $"WINDOW#{window}";

        var request = new PutItemRequest
        {
            TableName = config.TableName,
            Item = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = pk } },
                { "SK", new AttributeValue { S = sk } },
                { "StrategyId", new AttributeValue { S = strategyId } },
                { "Ticker", new AttributeValue { S = ticker } },
                { "Window", new AttributeValue { N = window.ToString() } },
                { "ExecutedAt", new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") } },
                { "TTL", new AttributeValue { N = ttl.ToString() } }
            },
            ConditionExpression = "attribute_not_exists(PK)"
        };

        try
        {
            await dynamoDB.PutItemAsync(request);
            logger.LogDebug("Recorded execution for strategy {StrategyId}, ticker {Ticker}, window {Window}",
                strategyId, ticker, window);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            logger.LogDebug("Duplicate execution detected for strategy {StrategyId}, ticker {Ticker}, window {Window}",
                strategyId, ticker, window);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error recording execution for strategy {StrategyId}, ticker {Ticker}, window {Window}",
                strategyId, ticker, window);
            throw;
        }
    }
}
