using Amazon.SQS;
using MarketViewer.Api.Config;
using MarketViewer.Contracts.Messages;
using MarketViewer.Contracts.Records.Scan;
using MarketViewer.Core.Services;
using System.Text.Json;

namespace MarketViewer.Api.Services;

/// <summary>
/// Publishes entry signals for the trading bots. The SQS message is the hot path;
/// the DynamoDB scan record is written asynchronously as an audit artifact and is
/// never allowed to slow down or fail the publish.
/// </summary>
public class SignalPublisher(
    IAmazonSQS sqsClient,
    SignalQueueConfig config,
    IScanRepository scanRepository,
    ILogger<SignalPublisher> logger)
{
    public async Task Publish(ScanRecord scanRecord)
    {
        if (config.Enabled && !string.IsNullOrEmpty(config.QueueUrl))
        {
            try
            {
                var message = new StrategySignalMessage
                {
                    StrategyHash = scanRecord.StrategyHash,
                    Window = scanRecord.Window,
                    Tickers = scanRecord.Tickers,
                    SignalAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                await sqsClient.SendMessageAsync(config.QueueUrl, JsonSerializer.Serialize(message));

                logger.LogInformation(
                    "Published signal for strategy hash {StrategyHash}, window {Window}, {TickerCount} tickers",
                    scanRecord.StrategyHash, scanRecord.Window, scanRecord.Tickers.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to publish signal for strategy hash {StrategyHash}, window {Window}",
                    scanRecord.StrategyHash, scanRecord.Window);
            }
        }

        // Fire-and-forget is fine at current scale (single-digit in-flight writes).
        // TODO: if fan-out grows (multi-tenant), replace with a bounded Channel<ScanRecord>
        // drained by a single background writer: backpressure (drop-oldest), shutdown
        // draining, and an observable queue depth.
        _ = PersistAuditRecord(scanRecord);
    }

    private async Task PersistAuditRecord(ScanRecord scanRecord)
    {
        try
        {
            await scanRepository.Create(scanRecord);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to persist scan audit record for strategy hash {StrategyHash}, window {Window}",
                scanRecord.StrategyHash, scanRecord.Window);
        }
    }
}
