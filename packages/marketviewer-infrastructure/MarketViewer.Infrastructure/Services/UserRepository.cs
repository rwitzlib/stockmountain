using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Records;
using MarketViewer.Core.Services;
using MarketViewer.Core.Utilities;
using MarketViewer.Infrastructure.Config;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net;

namespace MarketViewer.Infrastructure.Services;

public class UserRepository(UserConfig config, IAmazonDynamoDB dynamodb, ILogger<UserRepository> logger) : IUserRepository
{
    public async Task<bool> Put(UserRecord record)
    {
        logger.LogInformation("Storing user record for user {UserId} with role {Role}", record.Id, record.Role);

        var item = new Dictionary<string, AttributeValue>
        {
            { "Id", new AttributeValue { S = record.Id } },
            { "Role", new AttributeValue { S = record.Role.ToString() } },
            { "IsAdmin", new AttributeValue { BOOL = record.IsAdmin } },
            { "AvatarUrl", new AttributeValue { S = record.AvatarUrl ?? string.Empty } },
            { "IsPublic", new AttributeValue { S = record.IsPublic.ToString() } },
            { "Credits", new AttributeValue { N = record.Credits.ToString() } },
            { "Tokens", new AttributeValue { M = (record.Tokens ?? []).ToDictionary(kvp => kvp.Key.ToString(), kvp => new AttributeValue { S = kvp.Value }) } }
        };

        logger.LogDebug("DynamoDB item details: {@ItemDetails}", new
        {
            TableName = config.TableName,
            ItemCount = item.Count,
            HasTokens = record.Tokens?.Count > 0
        });

        var putItemRequest = new PutItemRequest
        {
            TableName = config.TableName,
            Item = item
        };

        await dynamodb.PutItemAsync(putItemRequest);

        return true;
    }

    public async Task<bool> Provision(UserRecord record)
    {
        logger.LogInformation("Provisioning user profile for user {UserId}", record.Id);

        var request = new UpdateItemRequest
        {
            TableName = config.TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "Id", new AttributeValue { S = record.Id } }
            },
            UpdateExpression = "SET AvatarUrl = :avatarUrl, #role = if_not_exists(#role, :role), IsAdmin = if_not_exists(IsAdmin, :isAdmin), IsPublic = if_not_exists(IsPublic, :isPublic), Credits = if_not_exists(Credits, :credits), Tokens = if_not_exists(Tokens, :tokens)",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                { "#role", "Role" }
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":avatarUrl", new AttributeValue { S = record.AvatarUrl ?? string.Empty } },
                { ":role", new AttributeValue { S = record.Role.ToString() } },
                { ":isAdmin", new AttributeValue { BOOL = record.IsAdmin } },
                { ":isPublic", new AttributeValue { S = record.IsPublic.ToString() } },
                { ":credits", new AttributeValue { N = record.Credits.ToString() } },
                { ":tokens", new AttributeValue { M = (record.Tokens ?? []).ToDictionary(kvp => kvp.Key.ToString(), kvp => new AttributeValue { S = kvp.Value }) } }
            }
        };

        await dynamodb.UpdateItemAsync(request);
        
        return false;
    }

    public async Task<UserRecord> Get(string id)
    {
        logger.LogInformation("Retrieving user record for user {UserId}", id);

        var response = await dynamodb.GetItemAsync(new GetItemRequest
        {
            TableName = config.TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "Id", new AttributeValue { S = id } }
            }
        });

        if (response.Item == null || response.Item.Count == 0)
        {
            logger.LogWarning("No user record found for user {UserId}", id);
            return null;
        }

        var userRecord = new UserRecord
        {
            Id = response.Item["Id"].S,
            Role = Enum.Parse<UserRole>(response.Item["Role"].S),
            IsAdmin = response.Item.TryGetValue("IsAdmin", out var isAdmin) && isAdmin.BOOL == true,
            AvatarUrl = response.Item["AvatarUrl"].S,
            IsPublic = bool.Parse(response.Item["IsPublic"].S),
            Credits = float.Parse(response.Item["Credits"].N),
            Tokens = response.Item.ContainsKey("Tokens")
                ? response.Item["Tokens"].M.ToDictionary(
                    kvp => Enum.Parse<IntegrationType>(kvp.Key),
                    kvp => kvp.Value.S)
                : []
        };

        logger.LogInformation("Successfully retrieved user record for user {UserId} with role {Role}",
            id, userRecord.Role);

        return userRecord;
    }

    public async Task<bool> TryDebitCredits(string id, float credits)
    {
        if (credits <= 0)
        {
            return true;
        }

        try
        {
            var request = new UpdateItemRequest
            {
                TableName = config.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "Id", new AttributeValue { S = id } }
                },
                UpdateExpression = "SET Credits = Credits - :credits",
                ConditionExpression = "attribute_exists(Id) AND Credits >= :credits",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":credits", new AttributeValue { N = credits.ToString(CultureInfo.InvariantCulture) } }
                }
            };

            var response = await dynamodb.UpdateItemAsync(request);
            return response.HttpStatusCode == HttpStatusCode.OK;
        }
        catch (ConditionalCheckFailedException)
        {
            logger.LogWarning("Unable to debit {Credits} credits from user {UserId}; balance changed or user does not exist", credits, id);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error debiting credits for user {UserId}: {Message}", id, ex.Message);
            return false;
        }
    }
}
