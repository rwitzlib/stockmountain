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
/// Monthly credit refill (plan 16 decision 1, extended by plan 17 decision 3). Monthly paid
/// subscribers refill on Stripe's invoice.paid webhook; this Lambda covers everyone else:
/// users whose role is Free (or the legacy "Basic" string) without an active subscription,
/// and annual subscribers (whose invoice.paid only fires once a year) — both get their
/// monthly allowance reset to the tier grant on the 1st. Grants come from tier config, never
/// from the stored MaxCredits — existing records have MaxCredits = 0 and would otherwise be
/// topped up to zero forever.
///
/// Annual anniversary-month overlap (this Lambda on the 1st plus invoice.paid on the renewal
/// date) is harmless: both are idempotent SETs of the same tier grant with independent
/// ledger keys, and the renewal's annual bonus rides the invoice event's own idempotency.
/// </summary>
public class MonthlyRefillService(
    IAmazonDynamoDB dynamodb,
    UserConfig userConfig,
    IBillingLedgerRepository ledger,
    ILogger<MonthlyRefillService> logger)
{
    public async Task<RefillResult> Run(string period, IReadOnlyDictionary<UserRole, float> tierGrants, bool dryRun)
    {
        var freeGrant = tierGrants.GetValueOrDefault(UserRole.Free, 0);

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
                ProjectionExpression = "Id, #role, SubscriptionStatus, BillingInterval",
                FilterExpression =
                    "((#role = :free OR #role = :basic) AND (attribute_not_exists(SubscriptionStatus) OR SubscriptionStatus <> :active))" +
                    " OR (SubscriptionStatus = :active AND BillingInterval = :year)",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#role", "Role" }
                },
                ExpressionAttributeValues = EligibilityValues(new Dictionary<string, AttributeValue>
                {
                    { ":year", new AttributeValue { S = "year" } }
                })
            });

            foreach (var item in response.Items ?? [])
            {
                var userId = item["Id"].S;
                result.Eligible++;

                // Active + yearly is the annual-subscriber branch; everything else the scan
                // returns is the free/legacy branch (past_due annuals fail the scan filter —
                // no refill during dunning, plan 16 decision 7).
                var isAnnual = item.TryGetValue("SubscriptionStatus", out var status) && status.S == "active"
                    && item.TryGetValue("BillingInterval", out var interval) && interval.S == "year";

                // Read once, tolerating an absent attribute — the annual scan clause
                // doesn't require Role, and indexing the item inside the catch would
                // throw the very exception being handled.
                var storedRole = item.TryGetValue("Role", out var roleAttribute) ? roleAttribute.S : null;

                var role = UserRole.Free;
                var grant = freeGrant;
                if (isAnnual)
                {
                    try
                    {
                        role = UserRoleParser.Parse(storedRole);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Annual subscriber {UserId} has missing or unparseable role '{Role}'; skipping", userId, storedRole);
                        result.Failed++;
                        continue;
                    }

                    grant = tierGrants.GetValueOrDefault(role, 0);
                    if (grant <= 0)
                    {
                        logger.LogError("Annual subscriber {UserId} has role {Role} with no configured tier grant; skipping", userId, role);
                        result.Failed++;
                        continue;
                    }
                }

                if (dryRun)
                {
                    logger.LogInformation("Dry run: would refill {Kind} user {UserId} to {Grant} credits for {Period}",
                        isAnnual ? "annual" : "free", userId, grant, period);
                    continue;
                }

                await RefillUser(userId, period, role, storedRole, grant, isAnnual, result);
            }

            exclusiveStartKey = response.LastEvaluatedKey;
        } while (exclusiveStartKey is { Count: > 0 });

        logger.LogInformation(
            "Monthly refill for {Period} complete: {Eligible} eligible, {Refilled} refilled, {AlreadyRefilled} already refilled, {Skipped} skipped (concurrent change), {Failed} failed{DryRun}",
            period, result.Eligible, result.Refilled, result.AlreadyRefilled, result.SkippedConcurrentChange, result.Failed,
            dryRun ? " [dry run]" : string.Empty);

        // Failing the invocation is what makes Lambda's async retry re-run us; a normal return
        // would leave the failed users unrefilled until next month's (different-period) run.
        // The full scan has completed by now, and a re-run is idempotent: applied ledger rows
        // no-op and pending ones resume.
        if (result.Failed > 0)
        {
            throw new RefillIncompleteException(
                $"Monthly refill for {period}: {result.Failed} of {result.Eligible} eligible users failed " +
                $"({result.Refilled} refilled, {result.AlreadyRefilled} already refilled, " +
                $"{result.SkippedConcurrentChange} skipped); failing the invocation so the retry resumes them.");
        }

        return result;
    }

    private async Task RefillUser(string userId, string period, UserRole role, string storedRole, float grant, bool isAnnual, RefillResult result)
    {
        // The ledger row is written pending-first and only marked applied after the credit
        // update succeeds. A crash or failure anywhere in between leaves a pending row that
        // the next run resumes — an applied row is the only thing treated as "already
        // refilled", so an interrupted pair can never silently cost a user their refill.
        // (Resuming re-applies an idempotent SET, so the worst double-run outcome is a
        // topped-up balance, never a lost one.)
        var eventKey = $"{period}#{userId}";

        try
        {
            var appended = await ledger.TryAppend(new BillingLedgerRecord
            {
                UserId = userId,
                EventKey = eventKey,
                Type = BillingLedgerEntryType.MonthlyRefill,
                AmountCents = 0,
                Credits = grant,
                Tier = role.ToString(),
                Description = isAnnual
                    ? $"Monthly annual-subscriber refill for {period}"
                    : $"Monthly free-tier refill for {period}",
                Status = BillingLedgerStatus.Pending
            });

            if (!appended && !await ledger.IsPending(userId, eventKey))
            {
                result.AlreadyRefilled++;
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to append refill ledger entry for user {UserId}, period {Period}", userId, period);
            result.Failed++;
            return;
        }

        try
        {
            // Eligibility is re-checked at write time: a user who subscribed (or, for the
            // annual branch, changed tier/interval or lapsed) between the scan and this
            // write must not have their grant clobbered with a stale one.
            await dynamodb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = userConfig.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "Id", new AttributeValue { S = userId } }
                },
                UpdateExpression = "SET Credits = :grant, MaxCredits = :grant",
                ConditionExpression = isAnnual
                    ? "attribute_exists(Id) AND #role = :roleValue AND SubscriptionStatus = :active AND BillingInterval = :year"
                    : "attribute_exists(Id) AND (#role = :free OR #role = :basic) AND (attribute_not_exists(SubscriptionStatus) OR SubscriptionStatus <> :active)",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#role", "Role" }
                },
                ExpressionAttributeValues = isAnnual
                    ? new Dictionary<string, AttributeValue>
                    {
                        { ":grant", new AttributeValue { N = grant.ToString(CultureInfo.InvariantCulture) } },
                        // The RAW stored role, not the parsed enum's name: a legacy alias
                        // ("Advanced" → Pro) would never equal the canonical name, so the
                        // condition would silently skip the user every month.
                        { ":roleValue", new AttributeValue { S = storedRole } },
                        { ":active", new AttributeValue { S = "active" } },
                        { ":year", new AttributeValue { S = "year" } }
                    }
                    : EligibilityValues(new Dictionary<string, AttributeValue>
                    {
                        { ":grant", new AttributeValue { N = grant.ToString(CultureInfo.InvariantCulture) } }
                    })
            });
        }
        catch (ConditionalCheckFailedException)
        {
            // No refill happened, so the pending row must not stand as one: remove it. If the
            // removal itself fails, the row stays pending and the next run lands back here —
            // self-healing, no manual cleanup.
            logger.LogWarning("User {UserId} stopped being refill-eligible between scan and write; removing pending ledger entry", userId);
            try
            {
                await ledger.Remove(userId, eventKey);
                result.SkippedConcurrentChange++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to remove pending ledger entry {EventKey} for user {UserId}; a re-run will retry", eventKey, userId);
                result.Failed++;
            }
            return;
        }
        catch (Exception ex)
        {
            // Leave the row pending: the retry resumes it.
            logger.LogError(ex, "Failed to apply refill for user {UserId}, period {Period}; ledger entry left pending for the re-run", userId, period);
            result.Failed++;
            return;
        }

        try
        {
            await ledger.MarkApplied(userId, eventKey);
            result.Refilled++;
        }
        catch (Exception ex)
        {
            // Credits are granted but the row is still pending; counted as failed so the retry
            // re-runs this user (the SET is idempotent) and marks the row applied.
            logger.LogError(ex, "Refill applied for user {UserId} but marking ledger entry {EventKey} applied failed; the re-run will settle it", userId, eventKey);
            result.Failed++;
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
