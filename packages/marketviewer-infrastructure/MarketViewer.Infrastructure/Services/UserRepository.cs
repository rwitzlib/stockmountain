using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Records;
using MarketViewer.Core.Services;
using MarketViewer.Core.Utilities;
using MarketViewer.Infrastructure.Config;
using Microsoft.Extensions.Logging;
using System.Net;

namespace MarketViewer.Infrastructure.Services;

public class UserRepository(UserConfig config, IAmazonDynamoDB dynamodb, ILogger<UserRepository> logger) : IUserRepository
{
    public async Task<bool> Put(UserRecord record)
    {
        return await logger.LogOperationAsync("PutUserRecord", async () =>
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

            var response = await dynamodb.PutItemAsync(putItemRequest);

            if (response.HttpStatusCode == HttpStatusCode.OK)
            {
                logger.LogInformation("Successfully stored user record for user {UserId}", record.Id);
                return true;
            }
            else
            {
                logger.LogError("DynamoDB returned status {StatusCode} when storing user {UserId}", response.HttpStatusCode, record.Id);
                return false;
            }
        }, additionalContext: new { UserId = record.Id, record.Role });
    }

    public async Task<bool> Provision(UserRecord record)
    {
        return await logger.LogOperationAsync("ProvisionUserRecord", async () =>
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

            var response = await dynamodb.UpdateItemAsync(request);

            if (response.HttpStatusCode == HttpStatusCode.OK)
            {
                logger.LogInformation("Successfully provisioned user profile for user {UserId}", record.Id);
                return true;
            }

            logger.LogError("DynamoDB returned status {StatusCode} when provisioning user {UserId}", response.HttpStatusCode, record.Id);
            return false;
        }, additionalContext: new { UserId = record.Id });
    }

    public async Task<UserRecord> Get(string id)
    {
        return await logger.LogOperationAsync("GetUserRecord", async () =>
        {
            logger.LogInformation("Retrieving user record for user {UserId}", id);

            var queryResponse = await dynamodb.GetItemAsync(new GetItemRequest
            {
                TableName = config.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "Id", new AttributeValue { S = id } }
                }
            });

            if (queryResponse.HttpStatusCode != HttpStatusCode.OK)
            {
                logger.LogError("DynamoDB returned status {StatusCode} when retrieving user {UserId}",
                    queryResponse.HttpStatusCode, id);
                return null;
            }

            if (queryResponse.Item == null || !queryResponse.IsItemSet)
            {
                logger.LogInformation("User record not found for user {UserId}", id);
                return null;
            }

            logger.LogDebug("Raw DynamoDB item contains {FieldCount} fields", queryResponse.Item.Count);

            var userRecord = new UserRecord
            {
                Id = queryResponse.Item["Id"].S,
                Role = Enum.Parse<UserRole>(queryResponse.Item["Role"].S),
                IsAdmin = queryResponse.Item.TryGetValue("IsAdmin", out var isAdmin) && isAdmin.BOOL == true,
                AvatarUrl = queryResponse.Item["AvatarUrl"].S,
                IsPublic = bool.Parse(queryResponse.Item["IsPublic"].S),
                Credits = float.Parse(queryResponse.Item["Credits"].N),
                Tokens = queryResponse.Item.ContainsKey("Tokens")
                    ? queryResponse.Item["Tokens"].M.ToDictionary(
                        kvp => Enum.Parse<IntegrationType>(kvp.Key),
                        kvp => kvp.Value.S)
                    : []
            };

            logger.LogInformation("Successfully retrieved user record for user {UserId} with role {Role}",
                id, userRecord.Role);

            return userRecord;
        }, additionalContext: new { UserId = id });
    }
}
