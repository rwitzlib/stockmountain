using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MarketViewer.Contracts.Caching;
using Microsoft.Extensions.Logging;
using Optimus.Infrastructure.Config;
using Optimus.Infrastructure.Utilities;
using System.Net;

namespace Optimus.Infrastructure.Repositories;

public class ScanResultsRepository(
    ScanConfig config,
    IAmazonDynamoDB dynamoDB,
    TickerCache tickerCache,
    ILogger<ScanResultsRepository> logger)
{
    public async Task<List<string>> GetTickersByHashAndWindow(string strategyHash, long window)
    {
        try
        {
            var request = new GetItemRequest
            {
                TableName = config.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"STRAT#{strategyHash}" } },
                    { "SK", new AttributeValue { S = $"WINDOW#{window}" } }
                }
            };

            var response = await dynamoDB.GetItemAsync(request);

            if (response.HttpStatusCode != HttpStatusCode.OK || response.Item == null || response.Item.Count == 0)
            {
                logger.LogWarning("Scan result not found for hash {StrategyHash} at window {Window}", strategyHash, window);
                return null;
            }

            var tickerBlob = response.Item["Tickers"].B.ToArray();
            var tickers = DecompressTickerBlob(tickerBlob);
            return tickers;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting scan results for hash {StrategyHash} at window {Window}", strategyHash, window);
            return null;
        }
    }

    private List<string> DecompressTickerBlob(byte[] compressedBytes)
    {
        var content = CompressionUtilities.DecompressBrotli(compressedBytes);
        var tickers = new List<string>();

        foreach (var line in content.Split('\n'))
        {
            if (!int.TryParse(line, out var id) || id >= tickerCache.IdToSymbol.Length)
            {
                continue;
            }

            var symbol = tickerCache.IdToSymbol[id];
            if (!string.IsNullOrEmpty(symbol))
            {
                tickers.Add(symbol);
            }
        }

        return tickers;
    }
}
