using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MarketViewer.Contracts.Records.Data;
using MarketViewer.Core.Utilities;
using MarketViewer.Infrastructure.Config;
using Microsoft.Extensions.Logging;
using System.Net;

namespace MarketViewer.Infrastructure.Services;

public class MetaRepository(MetaConfig config, IAmazonDynamoDB dynamoDB, ILogger<MetaRepository> logger)
{
    public async Task CreateUniverseSnapshotRecord(UniverseSnapshotRecord record)
    {
        try
        {
            var symbolBlob = BuildSymbolBlob(record.Symbols);
            var putItemRequest = new PutItemRequest
            {
                TableName = config.MetaTableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = "UNIVERSE" } },
                    { "SK", new AttributeValue { S = record.Version } },
                    { "MaxId", new AttributeValue { N = record.MaxId.ToString() } },
                    { "GeneratedAt", new AttributeValue { S = record.GeneratedAt.ToString("yyyy-MM-ddTHH:mm:ssZ") } },
                    { "IdToSymbolBlob", new AttributeValue { B = new MemoryStream(symbolBlob) } },
                }
            };
            var response = await dynamoDB.PutItemAsync(putItemRequest);
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                logger.LogError("Failed to create universe snapshot record for {Version}", record.Version);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating universe snapshot record for {Version}", record.Version);
        }
    }

    public async Task<UniverseMetaRecord> GetUniverseMeta(string version = null)
    {
        try
        {

            var request = new GetItemRequest
            {
                TableName = config.MetaTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = "UNIVERSE" } },
                    { "SK", new AttributeValue { S = version is null ? "CURRENT" : $"VERSION#{version}" } }
                }
            };

            var response = await dynamoDB.GetItemAsync(request);

            if (response.HttpStatusCode != HttpStatusCode.OK || response.Item.Count == 0)
            {
                logger.LogWarning("Universe meta not found in table {TableName}", config.MetaTableName);
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
                TableName = config.MetaTableName,
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

    public async Task<SecurityMasterRecord> PutSecurityRecord(SecurityMasterRecord record)
    {
        try
        {
            var updateRequest = new UpdateItemRequest
            {
                TableName = config.SecurityMasterTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = record.Symbol } }
                },
                UpdateExpression = "SET Active = :active, #T = :type, Market = :market, UpdatedAt = :updatedAt",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#T", "Type" }
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":active", new AttributeValue { BOOL = record.Active } },
                    { ":type", new AttributeValue { S = record.Type } },
                    { ":market", new AttributeValue { S = record.Market } },
                    { ":updatedAt", new AttributeValue { S = record.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ") } },
                },
                ReturnValues = "ALL_NEW"
            };

            if (record.Id is not null)
            {
                updateRequest.UpdateExpression += ", Id = :id";
                updateRequest.ExpressionAttributeValues.Add(":id", new AttributeValue { N = record.Id.ToString() });
            }

            var response = await dynamoDB.UpdateItemAsync(updateRequest);
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                logger.LogError("Failed to create security record for {Symbol}", record.Symbol);
                return null;
            }

            return new SecurityMasterRecord
            {
                Symbol = response.Attributes["PK"].S,
                Id = int.Parse(response.Attributes["Id"].N),
                Active = response.Attributes["Active"].BOOL.Value,
                Type = response.Attributes["Type"].S,
                Market = response.Attributes["Market"].S,
                UpdatedAt = DateTimeOffset.Parse(response.Attributes["UpdatedAt"].S)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating security record for {Symbol}", record.Symbol);
            return null;
        }
    }

    public async Task UpdateUniverseMeta(UniverseMetaRecord meta)
    {
        try
        {
            var putItemRequest = new PutItemRequest
            {
                TableName = config.MetaTableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = "UNIVERSE" } },
                    { "SK", new AttributeValue { S = "CURRENT" } },
                    { "Version", new AttributeValue { S = meta.Version } },
                    { "MaxId", new AttributeValue { N = meta.MaxId.ToString() } },
                    { "SymbolCount", new AttributeValue { N = meta.SymbolCount.ToString() } },
                    { "SnapshotKey", new AttributeValue { S = meta.SnapshotKey } }
                }
            };
            var response = await dynamoDB.PutItemAsync(putItemRequest);
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                logger.LogError("Failed to update universe meta for {Version}", meta.Version);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating universe meta for {Version}", meta.Version);
        }
    }

    #region Private Methods

    private static byte[] BuildSymbolBlob(string[] symbols)
    {
        var content = string.Join('\n', symbols);
        return CompressionUtilities.CompressBrotli(content);
    }

    private static string[] ParseSymbolBlob(MemoryStream blobStream)
    {
        var compressedBytes = blobStream.ToArray();
        var content = CompressionUtilities.DecompressBrotli(compressedBytes);
        return content.Split('\n');
    }

    #endregion
}
