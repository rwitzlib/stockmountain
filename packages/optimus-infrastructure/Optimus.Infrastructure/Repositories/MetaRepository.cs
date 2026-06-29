using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MarketViewer.Contracts.Records.Data;
using Microsoft.Extensions.Logging;
using Optimus.Infrastructure.Config;
using Optimus.Infrastructure.Utilities;
using System.Net;

namespace Optimus.Infrastructure.Repositories;

public class MetaRepository(MetaConfig config, IAmazonDynamoDB dynamoDB, ILogger<MetaRepository> logger)
{
    public async Task<UniverseMetaRecord> GetUniverseMeta(string version = null)
    {
        try
        {
            var request = new GetItemRequest
            {
                TableName = config.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = "UNIVERSE" } },
                    { "SK", new AttributeValue { S = version is null ? "CURRENT" : $"VERSION#{version}" } }
                }
            };

            var response = await dynamoDB.GetItemAsync(request);

            if (response.HttpStatusCode != HttpStatusCode.OK || response.Item.Count == 0)
            {
                logger.LogWarning("Universe meta not found in table {TableName}", config.TableName);
                return null;
            }

            return new UniverseMetaRecord
            {
                Version = response.Item["Version"].S,
                MaxId = int.Parse(response.Item["MaxId"].N),
                SymbolCount = int.Parse(response.Item["SymbolCount"].N),
                SnapshotKey = response.Item["SnapshotKey"].S
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting universe meta");
            return null;
        }
    }

    public async Task<UniverseSnapshotRecord> GetUniverseMetaSnapshot(UniverseMetaRecord meta)
    {
        try
        {
            var request = new GetItemRequest
            {
                TableName = config.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = "UNIVERSE" } },
                    { "SK", new AttributeValue { S = meta.SnapshotKey } }
                }
            };
            var response = await dynamoDB.GetItemAsync(request);
            if (response.HttpStatusCode != HttpStatusCode.OK || response.Item.Count == 0)
            {
                logger.LogWarning("Universe snapshot not found for {Version}", meta.Version);
                return null;
            }

            return new UniverseSnapshotRecord
            {
                Version = response.Item["SK"].S,
                MaxId = int.Parse(response.Item["MaxId"].N),
                GeneratedAt = DateTimeOffset.Parse(response.Item["GeneratedAt"].S),
                Symbols = ParseSymbolBlob(response.Item["IdToSymbolBlob"].B)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting universe snapshot for {Version}", meta.Version);
            return null;
        }
    }

    #region Private Methods

    private static string[] ParseSymbolBlob(MemoryStream blobStream)
    {
        var compressedBytes = blobStream.ToArray();
        var content = CompressionUtilities.DecompressBrotli(compressedBytes);
        return content.Split('\n');
    }

    #endregion
}
