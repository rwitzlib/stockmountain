using Amazon.SQS;
using Amazon.SQS.Model;
using MarketViewer.Contracts.Enums.Strategy;
using Optimus.Infrastructure.Config;
using Optimus.Infrastructure.Repositories;
using Optimus.Models;
using System.Text.Json;

namespace Optimus.Services;

public class ScanQueueConsumer(
    SqsConsumerConfig config,
    IAmazonSQS sqsClient,
    ScanResultsRepository scanResultsRepository,
    StrategyRepository strategyRepository,
    TradeExecutionService tradeExecutionService,
    ILogger<ScanQueueConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.Enabled)
        {
            logger.LogInformation("SQS Scan Consumer is disabled via feature flag.");
            return;
        }

        logger.LogInformation("SQS Scan Consumer started. Polling queue: {QueueUrl}", config.QueueUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var receiveRequest = new ReceiveMessageRequest
                {
                    QueueUrl = config.QueueUrl,
                    MaxNumberOfMessages = config.MaxNumberOfMessages,
                    WaitTimeSeconds = config.WaitTimeSeconds,
                    VisibilityTimeout = config.VisibilityTimeoutSeconds
                };

                var response = await sqsClient.ReceiveMessageAsync(receiveRequest, stoppingToken);

                if (response.Messages.Count == 0)
                {
                    continue;
                }

                logger.LogDebug("Received {Count} messages from SQS", response.Messages.Count);

                var tasks = response.Messages.Select(msg => ProcessMessage(msg, stoppingToken));
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error polling SQS: {Message}", ex.Message);
                await Task.Delay(5000, stoppingToken);
            }
        }

        logger.LogInformation("SQS Scan Consumer stopped.");
    }

    private async Task ProcessMessage(Message message, CancellationToken cancellationToken)
    {
        try
        {
            // Parse EventBridge Pipes DynamoDB stream record format
            var streamRecord = JsonSerializer.Deserialize<DynamoDbStreamRecord>(message.Body, JsonOptions);

            if (streamRecord?.DynamoDb?.Keys == null)
            {
                logger.LogWarning("Invalid SQS message format: {Body}", message.Body);
                await DeleteMessage(message);
                return;
            }

            // Only process INSERT events (new scan results)
            if (streamRecord.EventName != "INSERT")
            {
                logger.LogDebug("Skipping non-INSERT event: {EventName}", streamRecord.EventName);
                await DeleteMessage(message);
                return;
            }

            // Extract StrategyHash and Window from DynamoDB keys
            // PK format: STRAT#{strategyHash}
            // SK format: WINDOW#{timestamp}
            var pk = streamRecord.DynamoDb.Keys.PK?.S;
            var sk = streamRecord.DynamoDb.Keys.SK?.S;

            if (string.IsNullOrEmpty(pk) || string.IsNullOrEmpty(sk) ||
                !pk.StartsWith("STRAT#") || !sk.StartsWith("WINDOW#"))
            {
                logger.LogWarning("Invalid key format - PK: {PK}, SK: {SK}", pk, sk);
                await DeleteMessage(message);
                return;
            }

            var strategyHash = pk.Substring("STRAT#".Length);
            var windowStr = sk.Substring("WINDOW#".Length);

            if (!long.TryParse(windowStr, out var window))
            {
                logger.LogWarning("Invalid window format: {Window}", windowStr);
                await DeleteMessage(message);
                return;
            }

            logger.LogInformation("Processing scan event for hash {StrategyHash}, window {Window}",
                strategyHash, window);

            // 1. Fetch scan results (tickers) from DynamoDB
            var tickers = await scanResultsRepository.GetTickersByHashAndWindow(strategyHash, window);

            if (tickers == null || tickers.Count == 0)
            {
                logger.LogWarning("No scan results found for hash {StrategyHash} at window {Window}",
                    strategyHash, window);
                await DeleteMessage(message);
                return;
            }

            logger.LogInformation("Found {TickerCount} tickers for hash {StrategyHash}",
                tickers.Count, strategyHash);

            // 2. Query all strategies with this hash (via GSI)
            var strategies = await strategyRepository.ListByStrategyHash(strategyHash);
            var enabledStrategies = strategies.Where(s => s.State == StrategyStateType.Active).ToList();

            if (enabledStrategies.Count == 0)
            {
                logger.LogInformation("No enabled strategies found for hash {StrategyHash}", strategyHash);
                await DeleteMessage(message);
                return;
            }

            logger.LogInformation("Found {StrategyCount} enabled strategies for hash {StrategyHash}",
                enabledStrategies.Count, strategyHash);

            // 3. Execute trades for each strategy/ticker combination
            var executionTasks = new List<Task<bool>>();
            foreach (var strategy in enabledStrategies)
            {
                foreach (var ticker in tickers)
                {
                    executionTasks.Add(tradeExecutionService.ExecuteBuyIfNotDuplicate(
                        strategy,
                        ticker,
                        window));
                }
            }

            var results = await Task.WhenAll(executionTasks);
            var successCount = results.Count(r => r);

            logger.LogInformation(
                "Processed scan event: {SuccessCount}/{TotalCount} executions for hash {StrategyHash}",
                successCount, executionTasks.Count, strategyHash);

            // 4. Delete message after successful processing
            await DeleteMessage(message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing SQS message: {MessageId}", message.MessageId);
            // Don't delete - let visibility timeout expire for retry
        }
    }

    private async Task DeleteMessage(Message message)
    {
        try
        {
            await sqsClient.DeleteMessageAsync(config.QueueUrl, message.ReceiptHandle);
            logger.LogDebug("Deleted SQS message: {MessageId}", message.MessageId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting SQS message: {MessageId}", message.MessageId);
        }
    }
}
