using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Records;
using Microsoft.Extensions.Logging;
using Optimus.Infrastructure.Config;
using System.Net;

namespace Optimus.Infrastructure.Repositories;

public class UserRepository(UserConfig config, IAmazonDynamoDB dynamodb, ILogger<UserRepository> logger)
{
    public async Task<bool> Put(UserRecord record)
    {
        try
        {
            var item = new Dictionary<string, AttributeValue>
            {
                { "Id", new AttributeValue { S = record.Id } },
                { "Role", new AttributeValue { S = record.Role.ToString() } },
                { "AvatarUrl", new AttributeValue { S = record.AvatarUrl } },
                { "IsPublic", new AttributeValue { S = record.IsPublic.ToString() } },
                { "Credits", new AttributeValue { N = record.Credits.ToString() } },
                
            };
            var putItemRequest = new PutItemRequest
            {
                TableName = config.TableName,
                Item = item
            };
            var response = await dynamodb.PutItemAsync(putItemRequest);
            return response.HttpStatusCode == HttpStatusCode.OK;

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error putting user record: {message}", ex.Message);
            return false;
        }
    }

    public async Task<UserRecord> Get(string id)
    {
        try
        {
            var queryResponse = await dynamodb.GetItemAsync(new GetItemRequest
            {
                TableName = config.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "Id", new AttributeValue { S = id } }
                }
            });

            if (queryResponse.HttpStatusCode != HttpStatusCode.OK || queryResponse.Item == null || !queryResponse.IsItemSet)
            {
                return null;
            }

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

            return userRecord;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error getting user record: {message}", e.Message);
            return null;
        }
    }
}
