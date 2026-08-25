using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Records;
using MarketViewer.Core.Services;
using MarketViewer.Infrastructure.Config;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Billing.Lambda;

/// <summary>
/// Monthly free-tier credit refill (plan 16, decision 1). Paid subscribers refill on Stripe's
/// invoice.paid webhook; this Lambda covers everyone else: users whose role is Free (or the
/// legacy "Basic" string) without an active subscription get their monthly allowance reset to
/// the tier grant. Grants come from tier config, never from the stored MaxCredits — existing
/// records have MaxCredits = 0 and would otherwise be topped up to zero forever.
/// </summary>
public class MonthlyRefillService(
    IAmazonDynamoDB dynamodb,
    UserConfig userConfig,
    IBillingLedgerRepository ledger,
    ILogger<MonthlyRefillService> logger)
{
    public async Task<RefillResult> Run(string period, float freeGrant, bool dryRun)
    {
        // A non-positive grant means the Tiers config is missing; refusing to run beats
        // resetting every free user's balance to zero.
        if (freeGrant <= 0)
        {
            throw new InvalidOperationException(
                $"Free-tier monthly grant is {freeGrant}; refusing to run. Check the Tiers:Free:MonthlyCredits configuration.");
        }

        var result = new RefillResult { Period = period, DryRun = dryRun, FreeGrant = freeGrant };

        Dictionary<string, AttributeValue> exclusiveStartKey = null;
        do
        {
            var response = await dynamodb.ScanAsync(new ScanRequest
            {
                TableName = userConfig.TableName,
                ExclusiveStartKey = exclusiveStartKey,
                ProjectionExpression = "Id",
                FilterExpression =
                    "(#role = :free OR #role = :basic) AND (attribute_not_exists(SubscriptionStatus) OR SubscriptionStatus <> :active)",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#role", "Role" }
                },
                ExpressionAttributeValues = EligibilityValues()
            });

            foreach (var item in response.Items ?? [])
            {
                var userId = item["Id"].S;
                result.Eligible++;

                if (dryRun)
                {
                    logger.LogInformation("Dry run: would refill user {UserId} to {Grant} credits for {Period}", userId, freeGrant, period);
                    continue;
                }

                await RefillUser(userId, period, freeGrant, result);
            }

            exclusiveStartKey = response.LastEvaluatedKey;
        } while (exclusiveStartKey is { Count: > 0 });

        logger.LogInformation(
            "Monthly refill for {Period} complete: {Eligible} eligible, {Refilled} refilled, {AlreadyRefilled} already refilled, {Skipped} skipped (concurrent change), {Failed} failed{DryRun}",
            period, result.Eligible, result.Refilled, result.AlreadyRefilled, result.SkippedConcurrentChange, result.Failed,
            dryRun ? " [dry run]" : string.Empty);

        return result;
    }

    private async Task RefillUser(string userId, string period, float freeGrant, RefillResult result)
    {
        // Same ordering as the webhook handlers: the idempotent ledger append guards the
        // mutation, and a failed mutation rolls the ledger row back so a re-run can retry
        // the pair as a unit.
        var eventKey = $"{period}#{userId}";

        bool appended;
        try
        {
            appended = await ledger.TryAppend(new BillingLedgerRecord
            {
                UserId = userId,
                EventKey = eventKey,
                Type = BillingLedgerEntryType.MonthlyRefill,
                AmountCents = 0,
                Credits = freeGrant,
                Tier = UserRole.Free.ToString(),
                Description = $"Monthly free-tier refill for {period}"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to append refill ledger entry for user {UserId}, period {Period}", userId, period);
            result.Failed++;
            return;
        }

        if (!appended)
        {
            result.AlreadyRefilled++;
            return;
        }

        try
        {
            // Eligibility is re-checked at write time: a user who subscribed between the scan
            // and this write must not have their paid grant clobbered.
            await dynamodb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = userConfig.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "Id", new AttributeValue { S = userId } }
                },
                UpdateExpression = "SET Credits = :grant, MaxCredits = :grant",
                ConditionExpression =
                    "attribute_exists(Id) AND (#role = :free OR #role = :basic) AND (attribute_not_exists(SubscriptionStatus) OR SubscriptionStatus <> :active)",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#role", "Role" }
                },
                ExpressionAttributeValues = EligibilityValues(new Dictionary<string, AttributeValue>
                {
                    { ":grant", new AttributeValue { N = freeGrant.ToString(CultureInfo.InvariantCulture) } }
                })
            });

            result.Refilled++;
        }
        catch (ConditionalCheckFailedException)
        {
            logger.LogWarning("User {UserId} stopped being refill-eligible between scan and write; rolling back ledger entry", userId);
            await TryRemoveLedgerEntry(userId, eventKey);
            result.SkippedConcurrentChange++;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply refill for user {UserId}, period {Period}; rolling back ledger entry", userId, period);
            await TryRemoveLedgerEntry(userId, eventKey);
            result.Failed++;
        }
    }

    private async Task TryRemoveLedgerEntry(string userId, string eventKey)
    {
        try
        {
            await ledger.Remove(userId, eventKey);
        }
        catch (Exception ex)
        {
            // Orphaned ledger row: the user shows a refill that was not applied. Left in place,
            // it blocks the re-run for this period, so it needs surfacing for manual cleanup.
            logger.LogError(ex, "Failed to roll back ledger entry {EventKey} for user {UserId}; remove it manually before re-running", eventKey, userId);
        }
    }

    private static Dictionary<string, AttributeValue> EligibilityValues(Dictionary<string, AttributeValue> values = null)
    {
        values ??= [];
        values.Add(":free", new AttributeValue { S = UserRole.Free.ToString() });
        values.Add(":basic", new AttributeValue { S = "Basic" });
        values.Add(":active", new AttributeValue { S = "active" });
        return values;
    }
}
