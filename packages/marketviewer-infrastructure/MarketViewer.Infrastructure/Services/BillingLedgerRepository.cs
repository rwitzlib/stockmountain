using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MarketViewer.Contracts.Records;
using MarketViewer.Core.Services;
using MarketViewer.Infrastructure.Config;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace MarketViewer.Infrastructure.Services;

public class BillingLedgerRepository(BillingLedgerConfig config, IAmazonDynamoDB dynamodb, ILogger<BillingLedgerRepository> logger) : IBillingLedgerRepository
{
    public async Task<bool> TryAppend(BillingLedgerRecord entry)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            { "UserId", new AttributeValue { S = entry.UserId } },
            { "EventKey", new AttributeValue { S = entry.EventKey } },
            { "Type", new AttributeValue { S = entry.Type } },
            { "AmountCents", new AttributeValue { N = entry.AmountCents.ToString(CultureInfo.InvariantCulture) } },
            { "Credits", new AttributeValue { N = entry.Credits.ToString(CultureInfo.InvariantCulture) } }
        };

        AddIfPresent(item, "StripeEventId", entry.StripeEventId);
        AddIfPresent(item, "StripeInvoiceId", entry.StripeInvoiceId);
        AddIfPresent(item, "StripePaymentIntentId", entry.StripePaymentIntentId);
        AddIfPresent(item, "Tier", entry.Tier);
        AddIfPresent(item, "Description", entry.Description);

        try
        {
            await dynamodb.PutItemAsync(new PutItemRequest
            {
                TableName = config.TableName,
                Item = item,
                ConditionExpression = "attribute_not_exists(EventKey)"
            });

            logger.LogInformation(
                "Billing ledger: {Type} for user {UserId} ({Credits} credits, {AmountCents} cents, key {EventKey})",
                entry.Type, entry.UserId, entry.Credits, entry.AmountCents, entry.EventKey);

            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            logger.LogInformation(
                "Billing ledger entry {EventKey} for user {UserId} already exists; treating as redelivery",
                entry.EventKey, entry.UserId);
            return false;
        }
    }

    public async Task Remove(string userId, string eventKey)
    {
        logger.LogWarning("Rolling back billing ledger entry {EventKey} for user {UserId}", eventKey, userId);

        await dynamodb.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = config.TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "UserId", new AttributeValue { S = userId } },
                { "EventKey", new AttributeValue { S = eventKey } }
            }
        });
    }

    private static void AddIfPresent(Dictionary<string, AttributeValue> item, string name, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            item.Add(name, new AttributeValue { S = value });
        }
    }
}
