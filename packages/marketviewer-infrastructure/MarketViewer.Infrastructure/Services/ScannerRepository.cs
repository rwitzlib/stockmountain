using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Models.Strategy;
using MarketViewer.Core.Services;
using MarketViewer.Infrastructure.Config;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace MarketViewer.Infrastructure.Services;

/// <summary>
/// Scanners share the strategy table under the SCANNER# key prefix — the UserIndex GSI
/// serves both entity types, with begins_with(PK) filters keeping the listings disjoint.
/// </summary>
public class ScannerRepository(StrategyConfig config, IAmazonDynamoDB dynamoDb, ILogger<ScannerRepository> logger) : IScannerRepository
{
    private const string KeyPrefix = "SCANNER#";

    public async Task<ScannerDto> Create(ScannerDto scanner)
    {
        try
        {
            var request = new PutItemRequest
            {
                TableName = config.TableName,
                Item = MapToDynamoDbItem(scanner)
            };

            var response = await dynamoDb.PutItemAsync(request);

            logger.LogInformation("Put scanner with ID {Id}, response status: {StatusCode}", scanner.Id, response.HttpStatusCode);

            return scanner;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error putting scanner with ID {Id}", scanner.Id);
            return null;
        }
    }

    public async Task<ScannerDto> Get(string id)
    {
        try
        {
            var request = new GetItemRequest
            {
                TableName = config.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"{KeyPrefix}{id}" } },
                    { "SK", new AttributeValue { S = "CONFIG" } }
                }
            };

            var response = await dynamoDb.GetItemAsync(request);
            return response.Item is not { Count: > 0 } ? null : MapToScannerDto(response.Item);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting scanner with ID {Id}", id);
            return null;
        }
    }

    public async Task<IEnumerable<ScannerDto>> ListByUser(string userId)
    {
        try
        {
            var request = new QueryRequest
            {
                TableName = config.TableName,
                IndexName = config.UserIndexName,
                KeyConditionExpression = "UserId = :userId",
                FilterExpression = "begins_with(PK, :prefix)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":userId", new AttributeValue { S = userId } },
                    { ":prefix", new AttributeValue { S = KeyPrefix } }
                }
            };

            var response = await dynamoDb.QueryAsync(request);
            return (response.Items ?? []).Select(MapToScannerDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing scanners for user {UserId}", userId);
            return [];
        }
    }

    public async Task<ScannerDto> Update(ScannerDto scanner)
    {
        try
        {
            var request = new PutItemRequest
            {
                TableName = config.TableName,
                Item = MapToDynamoDbItem(scanner)
            };

            var response = await dynamoDb.PutItemAsync(request);

            logger.LogInformation("Put scanner with ID {Id}, response status: {StatusCode}", scanner.Id, response.HttpStatusCode);

            return scanner;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error putting scanner with ID {Id}", scanner.Id);
            return null;
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
                    { "PK", new AttributeValue { S = $"{KeyPrefix}{id}" } },
                    { "SK", new AttributeValue { S = "CONFIG" } }
                }
            };

            var response = await dynamoDb.DeleteItemAsync(request);

            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                logger.LogError("Failed to delete scanner with ID {Id}", id);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting scanner with ID {Id}", id);
            return false;
        }
    }

    #region Private Methods

    private static ScannerDto MapToScannerDto(Dictionary<string, AttributeValue> item)
    {
        return new ScannerDto
        {
            Id = item["PK"].S.Split(KeyPrefix)[1],
            UserId = item["UserId"].S,
            Name = item["Name"].S,
            EntrySettings = JsonSerializer.Deserialize<StrategyEntrySettings>(Document.FromAttributeMap(item["EntrySettings"].M).ToJson())
        };
    }

    private static Dictionary<string, AttributeValue> MapToDynamoDbItem(ScannerDto scanner)
    {
        return new Dictionary<string, AttributeValue>
        {
            { "PK", new AttributeValue { S = $"{KeyPrefix}{scanner.Id}" } },
            { "SK", new AttributeValue { S = "CONFIG" } },
            { "UserId", new AttributeValue { S = scanner.UserId } },
            { "Name", new AttributeValue { S = scanner.Name } },
            { "EntrySettings", new AttributeValue { M = Document.FromJson(JsonSerializer.Serialize(scanner.EntrySettings)).ToAttributeMap() } }
        };
    }

    #endregion
}
