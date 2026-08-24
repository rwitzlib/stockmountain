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
            { "Credits", new AttributeValue { N = record.Credits.ToString(CultureInfo.InvariantCulture) } },
            { "MaxCredits", new AttributeValue { N = record.MaxCredits.ToString(CultureInfo.InvariantCulture) } },
            { "PurchasedCredits", new AttributeValue { N = record.PurchasedCredits.ToString(CultureInfo.InvariantCulture) } },
            { "Tokens", new AttributeValue { M = (record.Tokens ?? []).ToDictionary(kvp => kvp.Key.ToString(), kvp => new AttributeValue { S = kvp.Value }) } }
        };

        if (!string.IsNullOrEmpty(record.StripeCustomerId))
        {
            item.Add("StripeCustomerId", new AttributeValue { S = record.StripeCustomerId });
        }

        if (!string.IsNullOrEmpty(record.SubscriptionStatus))
        {
            item.Add("SubscriptionStatus", new AttributeValue { S = record.SubscriptionStatus });
        }

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
            UpdateExpression = "SET AvatarUrl = :avatarUrl, #role = if_not_exists(#role, :role), IsAdmin = if_not_exists(IsAdmin, :isAdmin), IsPublic = if_not_exists(IsPublic, :isPublic), Credits = if_not_exists(Credits, :credits), MaxCredits = if_not_exists(MaxCredits, :maxCredits), PurchasedCredits = if_not_exists(PurchasedCredits, :purchasedCredits), Tokens = if_not_exists(Tokens, :tokens)",
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
                { ":credits", new AttributeValue { N = record.Credits.ToString(CultureInfo.InvariantCulture) } },
                { ":maxCredits", new AttributeValue { N = record.MaxCredits.ToString(CultureInfo.InvariantCulture) } },
                { ":purchasedCredits", new AttributeValue { N = record.PurchasedCredits.ToString(CultureInfo.InvariantCulture) } },
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
            Role = UserRoleParser.Parse(response.Item["Role"].S),
            IsAdmin = response.Item.TryGetValue("IsAdmin", out var isAdmin) && isAdmin.BOOL == true,
            AvatarUrl = response.Item["AvatarUrl"].S,
            IsPublic = bool.Parse(response.Item["IsPublic"].S),
            Credits = float.Parse(response.Item["Credits"].N, CultureInfo.InvariantCulture),
            MaxCredits = response.Item.TryGetValue("MaxCredits", out var maxCredits) ? float.Parse(maxCredits.N, CultureInfo.InvariantCulture) : 0,
            PurchasedCredits = response.Item.TryGetValue("PurchasedCredits", out var purchasedCredits) ? float.Parse(purchasedCredits.N, CultureInfo.InvariantCulture) : 0,
            StripeCustomerId = response.Item.TryGetValue("StripeCustomerId", out var stripeCustomerId) ? stripeCustomerId.S : null,
            SubscriptionStatus = response.Item.TryGetValue("SubscriptionStatus", out var subscriptionStatus) ? subscriptionStatus.S : null,
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

        // Monthly credits are spent first, then purchased top-up credits. The split needs a
        // read, so the cross-balance path uses optimistic concurrency (retry once if either
        // balance moved between read and write); the monthly-only path stays a single
        // atomic conditional decrement.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var user = await Get(id);
            if (user is null)
            {
                return false;
            }

            if (user.Credits + user.PurchasedCredits < credits)
            {
                logger.LogWarning(
                    "Unable to debit {Credits} credits from user {UserId}; insufficient balance ({Monthly} monthly + {Purchased} purchased)",
                    credits, id, user.Credits, user.PurchasedCredits);
                return false;
            }

            try
            {
                if (user.Credits >= credits)
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
                else
                {
                    var newPurchased = user.PurchasedCredits - (credits - user.Credits);
                    var request = new UpdateItemRequest
                    {
                        TableName = config.TableName,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            { "Id", new AttributeValue { S = id } }
                        },
                        UpdateExpression = "SET Credits = :zero, PurchasedCredits = :newPurchased",
                        ConditionExpression = "attribute_exists(Id) AND Credits = :expectedCredits AND PurchasedCredits = :expectedPurchased",
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            { ":zero", new AttributeValue { N = "0" } },
                            { ":newPurchased", new AttributeValue { N = newPurchased.ToString(CultureInfo.InvariantCulture) } },
                            { ":expectedCredits", new AttributeValue { N = user.Credits.ToString(CultureInfo.InvariantCulture) } },
                            { ":expectedPurchased", new AttributeValue { N = user.PurchasedCredits.ToString(CultureInfo.InvariantCulture) } }
                        }
                    };

                    var response = await dynamodb.UpdateItemAsync(request);
                    return response.HttpStatusCode == HttpStatusCode.OK;
                }
            }
            catch (ConditionalCheckFailedException)
            {
                logger.LogWarning(
                    "Debit of {Credits} credits for user {UserId} hit a concurrent balance change (attempt {Attempt}); retrying",
                    credits, id, attempt + 1);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error debiting credits for user {UserId}: {Message}", id, ex.Message);
                return false;
            }
        }

        logger.LogWarning("Unable to debit {Credits} credits from user {UserId}; balance kept changing", credits, id);
        return false;
    }

    public async Task<bool> SetStripeCustomerId(string id, string stripeCustomerId)
    {
        return await UpdateExisting(id, new UpdateItemRequest
        {
            UpdateExpression = "SET StripeCustomerId = :customerId",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":customerId", new AttributeValue { S = stripeCustomerId } }
            }
        });
    }

    public async Task<bool> ApplySubscriptionGrant(string id, UserRole role, float monthlyGrant)
    {
        logger.LogInformation("Applying {Role} subscription grant of {Grant} credits to user {UserId}", role, monthlyGrant, id);

        return await UpdateExisting(id, new UpdateItemRequest
        {
            UpdateExpression = "SET #role = :role, Credits = :grant, MaxCredits = :grant, SubscriptionStatus = :status",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                { "#role", "Role" }
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":role", new AttributeValue { S = role.ToString() } },
                { ":grant", new AttributeValue { N = monthlyGrant.ToString(CultureInfo.InvariantCulture) } },
                { ":status", new AttributeValue { S = "active" } }
            }
        });
    }

    public async Task<bool> ApplyUpgradeGrant(string id, UserRole role, float monthlyGrant, float creditsDelta)
    {
        logger.LogInformation("Applying upgrade to {Role} for user {UserId} (+{Delta} credits)", role, id, creditsDelta);

        return await UpdateExisting(id, new UpdateItemRequest
        {
            UpdateExpression = "SET #role = :role, MaxCredits = :grant, SubscriptionStatus = :status ADD Credits :delta",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                { "#role", "Role" }
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":role", new AttributeValue { S = role.ToString() } },
                { ":grant", new AttributeValue { N = monthlyGrant.ToString(CultureInfo.InvariantCulture) } },
                { ":status", new AttributeValue { S = "active" } },
                { ":delta", new AttributeValue { N = creditsDelta.ToString(CultureInfo.InvariantCulture) } }
            }
        });
    }

    public async Task<bool> AddPurchasedCredits(string id, float credits)
    {
        logger.LogInformation("Adding {Credits} purchased credits to user {UserId}", credits, id);

        return await UpdateExisting(id, new UpdateItemRequest
        {
            UpdateExpression = "ADD PurchasedCredits :credits",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":credits", new AttributeValue { N = credits.ToString(CultureInfo.InvariantCulture) } }
            }
        });
    }

    public async Task<bool> SetSubscriptionStatus(string id, string status)
    {
        return await UpdateExisting(id, new UpdateItemRequest
        {
            UpdateExpression = "SET SubscriptionStatus = :status",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":status", new AttributeValue { S = status } }
            }
        });
    }

    public async Task<bool> CancelSubscription(string id, float freeGrant)
    {
        // Clamping Credits to the Free grant needs a read, so this uses the same
        // optimistic-concurrency pattern as the split-balance debit path.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var user = await Get(id);
            if (user is null)
            {
                return false;
            }

            var newCredits = Math.Min(user.Credits, freeGrant);

            try
            {
                var response = await dynamodb.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = config.TableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        { "Id", new AttributeValue { S = id } }
                    },
                    UpdateExpression = "SET #role = :role, Credits = :credits, MaxCredits = :maxCredits, SubscriptionStatus = :status",
                    ConditionExpression = "attribute_exists(Id) AND Credits = :expectedCredits",
                    ExpressionAttributeNames = new Dictionary<string, string>
                    {
                        { "#role", "Role" }
                    },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":role", new AttributeValue { S = UserRole.Free.ToString() } },
                        { ":credits", new AttributeValue { N = newCredits.ToString(CultureInfo.InvariantCulture) } },
                        { ":maxCredits", new AttributeValue { N = freeGrant.ToString(CultureInfo.InvariantCulture) } },
                        { ":status", new AttributeValue { S = "canceled" } },
                        { ":expectedCredits", new AttributeValue { N = user.Credits.ToString(CultureInfo.InvariantCulture) } }
                    }
                });

                logger.LogInformation("Cancelled subscription for user {UserId}; credits clamped to {Credits}", id, newCredits);
                return response.HttpStatusCode == HttpStatusCode.OK;
            }
            catch (ConditionalCheckFailedException)
            {
                logger.LogWarning("Subscription cancel for user {UserId} hit a concurrent balance change (attempt {Attempt}); retrying", id, attempt + 1);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error cancelling subscription for user {UserId}: {Message}", id, ex.Message);
                return false;
            }
        }

        logger.LogWarning("Unable to cancel subscription for user {UserId}; balance kept changing", id);
        return false;
    }

    private async Task<bool> UpdateExisting(string id, UpdateItemRequest request)
    {
        request.TableName = config.TableName;
        request.Key = new Dictionary<string, AttributeValue>
        {
            { "Id", new AttributeValue { S = id } }
        };
        request.ConditionExpression = string.IsNullOrEmpty(request.ConditionExpression)
            ? "attribute_exists(Id)"
            : request.ConditionExpression;

        try
        {
            var response = await dynamodb.UpdateItemAsync(request);
            return response.HttpStatusCode == HttpStatusCode.OK;
        }
        catch (ConditionalCheckFailedException)
        {
            logger.LogWarning("Update for user {UserId} failed; user record does not exist", id);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating user {UserId}: {Message}", id, ex.Message);
            return false;
        }
    }
}
