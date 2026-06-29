using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Records.Scan;
using MarketViewer.Core.Services;
using MarketViewer.Core.Utilities;
using MarketViewer.Infrastructure.Config;
using Microsoft.Extensions.Logging;
using System.Net;

namespace MarketViewer.Infrastructure.Services;

public class ScanRepository(ScanConfig config, IAmazonDynamoDB dynamoDb, TickerCache tickerCache, ILogger<StrategyRepository> logger) : IScanRepository
{
    private const int EXPIRY = 5;

    public async Task<ScanRecord> Create(ScanRecord scan)
    {
        try
        {
            var symbolBlob = BuildSymbolBlob(scan.Tickers);
            var putItemRequest = new PutItemRequest
            {
                TableName = config.TableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"STRAT#{scan.StrategyHash}" } },
                    { "SK", new AttributeValue { S = $"WINDOW#{scan.Window}" } },
                    { "Tickers", new AttributeValue { B = new MemoryStream(symbolBlob) } },
                    { "Expiry", new AttributeValue { N = DateTimeOffset.Now.AddMinutes(EXPIRY).ToUnixTimeSeconds().ToString() } },
                    { "Duration", new AttributeValue { N = scan.TimeElapsed.ToString() } },
                    { "Count", new AttributeValue { N = scan.Tickers.Count.ToString() } },
                    { "CreatedAt", new AttributeValue { N = DateTimeOffset.Now.ToUnixTimeSeconds().ToString() } },
                    { "CadenceSec", new AttributeValue { N = scan.CadenceSec.ToString() } }
                }
            };

            var response = await dynamoDb.PutItemAsync(putItemRequest);

            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                logger.LogError("Failed to create scan record for strategy {StrategyHash} at window {Window}. DynamoDB response code: {StatusCode}", scan.StrategyHash, scan.Window, response.HttpStatusCode);
                return null;
            }

            return scan;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Exception occurred while creating scan record for strategy {StrategyHash} at window {Window}", scan.StrategyHash, scan.Window);
            return null;
        }
    }

    public async Task<ScanRecord> Get(string strategyHash, long window)
    {
        try
        {
            var getItemRequest = new GetItemRequest
            {
                TableName = config.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"STRAT#{strategyHash}" } },
                    { "SK", new AttributeValue { S = $"WINDOW#{window}" } }
                }
            };

            var response = await dynamoDb.GetItemAsync(getItemRequest);

            if (response.HttpStatusCode != HttpStatusCode.OK || response.Item == null || response.Item.Count == 0)
            {
                logger.LogWarning("Scan record not found for strategy {StrategyHash} at window {Window}", strategyHash, window);
                return null;
            }

            var item = response.Item;

            var scanRecord = new ScanRecord
            {
                StrategyHash = strategyHash,
                Window = long.Parse(item["SK"].S.Split("WINDOW#")[1]),
                Tickers = ParseSymbolBlob(item["Tickers"].B)
            };

            return scanRecord;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Exception occurred while retrieving scan record for strategy {StrategyHash} at window {Window}", strategyHash, window);
            return null;
        }
    }

    #region Private Methods

    private byte[] BuildSymbolBlob(List<string> symbols)
    {
        var ids = symbols.Select(q => tickerCache.SymbolToId.FirstOrDefault(w => w.Key == q).Value).Where(q => q != 0);
        var content = string.Join('\n', ids);
        return CompressionUtilities.CompressBrotli(content);
    }

    private List<string> ParseSymbolBlob(MemoryStream blobStream)
    {
        var compressedBytes = blobStream.ToArray();
        var content = CompressionUtilities.DecompressBrotli(compressedBytes);

        var symbols = new List<string>();
        foreach (var line in content.Split('\n'))
        {
            if (!int.TryParse(line, out var id) || id >= tickerCache.IdToSymbol.Length)
            {
                continue;
            }
            symbols.Add(tickerCache.IdToSymbol[id]);
        }
        return symbols;
    }

    #endregion
}
