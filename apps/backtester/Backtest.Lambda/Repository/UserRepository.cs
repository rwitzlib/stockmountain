using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Backtest.Lambda.Config;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Records;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net;

namespace Backtest.Lambda.Repository;

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
                { "AvatarUrl", new AttributeValue { S = record.AvatarUrl ?? string.Empty } },
                { "IsPublic", new AttributeValue { S = record.IsPublic.ToString() } },
                { "Credits", new AttributeValue { N = record.Credits.ToString() } }
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
                AvatarUrl = queryResponse.Item["AvatarUrl"].S,
                IsPublic = bool.Parse(queryResponse.Item["IsPublic"].S),
                Credits = float.Parse(queryResponse.Item["Credits"].N),
            };

            return userRecord;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error getting user record: {message}", e.Message);
            return null;
        }
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
