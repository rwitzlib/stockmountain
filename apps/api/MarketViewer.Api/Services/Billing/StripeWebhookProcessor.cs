using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Records;
using MarketViewer.Core.Services;
using Newtonsoft.Json.Linq;
using Stripe;
using Stripe.Checkout;

namespace MarketViewer.Api.Services.Billing;

/// <summary>
/// Applies verified Stripe webhook events to the user store and billing ledger. Every
/// credit/role mutation is guarded by an idempotent ledger append (the Stripe event id is in
/// the ledger sort key), so webhook redelivery is a no-op. Handlers tolerate out-of-order
/// delivery: the user is resolved from event metadata first, then from the Stripe customer's
/// "userId" metadata, so invoice.paid can win the race against checkout.session.completed.
/// </summary>
public class StripeWebhookProcessor(
    IUserRepository userRepository,
    IBillingLedgerRepository ledger,
    IStripeGateway stripeGateway,
    BillingCatalog catalog,
    ILogger<StripeWebhookProcessor> logger)
{
    /// <summary>
    /// Returns true when the event was handled (or intentionally skipped); false when a
    /// mutation failed and Stripe should redeliver.
    /// </summary>
    public async Task<bool> Process(Event stripeEvent)
    {
        switch (stripeEvent.Type)
        {
            case EventTypes.CheckoutSessionCompleted:
                return await HandleCheckoutSessionCompleted(stripeEvent);
            case EventTypes.InvoicePaid:
                return await HandleInvoicePaid(stripeEvent);
            case EventTypes.CustomerSubscriptionUpdated:
                return await HandleSubscriptionUpdated(stripeEvent);
            case EventTypes.CustomerSubscriptionDeleted:
                return await HandleSubscriptionDeleted(stripeEvent);
            case EventTypes.InvoicePaymentFailed:
                return await HandleInvoicePaymentFailed(stripeEvent);
            case EventTypes.ChargeRefunded:
                return await HandleChargeRefunded(stripeEvent);
            default:
                logger.LogDebug("Ignoring Stripe webhook event type {EventType}", stripeEvent.Type);
                return true;
        }
    }

    private async Task<bool> HandleCheckoutSessionCompleted(Event stripeEvent)
    {
        var session = (Session)stripeEvent.Data.Object;
        var userId = await ResolveUserId(stripeEvent, session.ClientReferenceId, session.CustomerId);
        if (userId is null)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(session.CustomerId))
        {
            var user = await userRepository.Get(userId);
            if (user is not null && string.IsNullOrEmpty(user.StripeCustomerId))
            {
                await userRepository.SetStripeCustomerId(userId, session.CustomerId);
            }
            else if (user is not null && user.StripeCustomerId != session.CustomerId)
            {
                logger.LogWarning(
                    "Checkout session {SessionId} completed on Stripe customer {SessionCustomerId} but user {UserId} is linked to {StoredCustomerId}",
                    session.Id, session.CustomerId, userId, user.StripeCustomerId);
            }
        }

        if (session.Mode != "payment")
        {
            // Subscription checkouts only link the customer here; the credit grant rides
            // the invoice.paid event (money must actually arrive first).
            return true;
        }

        var packId = session.Metadata?.GetValueOrDefault("pack");
        if (!catalog.TryGetPackCredits(packId, out var credits))
        {
            logger.LogError("Stripe event {EventId}: checkout session {SessionId} has unknown pack '{PackId}'",
                stripeEvent.Id, session.Id, packId);
            return true;
        }

        return await ApplyLedgeredMutation(
            new BillingLedgerRecord
            {
                UserId = userId,
                EventKey = EventKey(stripeEvent),
                Type = BillingLedgerEntryType.TopupPurchase,
                AmountCents = session.AmountTotal ?? 0,
                Credits = credits,
                StripeEventId = stripeEvent.Id,
                StripePaymentIntentId = session.PaymentIntentId,
                Description = $"Credit pack {packId}"
            },
            () => userRepository.AddPurchasedCredits(userId, credits));
    }

    private async Task<bool> HandleInvoicePaid(Event stripeEvent)
    {
        var invoice = (Invoice)stripeEvent.Data.Object;
        var subscriptionDetails = invoice.Parent?.SubscriptionDetails;
        var userId = await ResolveUserId(stripeEvent, subscriptionDetails?.Metadata?.GetValueOrDefault("userId"), invoice.CustomerId);
        if (userId is null)
        {
            return true;
        }

        if (!TryResolveTierFromInvoice(invoice, out var tier, out var interval))
        {
            logger.LogError("Stripe event {EventId}: invoice {InvoiceId} has no line matching a configured tier price",
                stripeEvent.Id, invoice.Id);
            return true;
        }

        // An upgrade's prorated invoice (billing_reason subscription_update) is money only —
        // the credit delta was already granted by customer.subscription.updated. The grant
        // path also SETs BillingInterval from the paying price: that's what flips a
        // period-end annual→monthly downgrade back to "month" at renewal. A scheduled
        // downgrade normally lands as a subscription_cycle renewal, but if Stripe bills the
        // phase switch as subscription_update the lower tier must still be applied here —
        // nothing else would ever drop the role.
        var isUpgradeProration = invoice.BillingReason == "subscription_update"
            && !await IsDowngradeFor(userId, tier);
        var grant = catalog.GetMonthlyGrant(tier);

        var paymentApplied = await ApplyLedgeredMutation(
            new BillingLedgerRecord
            {
                UserId = userId,
                EventKey = EventKey(stripeEvent),
                Type = BillingLedgerEntryType.SubscriptionPayment,
                AmountCents = invoice.AmountPaid,
                Credits = isUpgradeProration ? 0 : grant,
                StripeEventId = stripeEvent.Id,
                StripeInvoiceId = invoice.Id,
                Tier = tier.ToString(),
                Description = $"Subscription invoice ({invoice.BillingReason})"
            },
            () => isUpgradeProration
                ? Task.FromResult(true)
                : userRepository.ApplySubscriptionGrant(userId, tier, grant, interval));

        if (!paymentApplied)
        {
            return false;
        }

        // Annual commitment bonus (plan 17 decision 4): one month's grant into the
        // never-expiring purchased balance on annual signup and every annual renewal.
        // The proration invoice after a monthly→annual switch grants nothing here —
        // that switch's bonus rides customer.subscription.updated instead.
        if (interval != BillingInterval.Year
            || invoice.BillingReason is not ("subscription_create" or "subscription_cycle"))
        {
            return true;
        }

        return await GrantAnnualBonus(stripeEvent, userId, tier, invoice.Id, $"Annual bonus ({invoice.BillingReason})");
    }

    private async Task<bool> HandleSubscriptionUpdated(Event stripeEvent)
    {
        var subscription = (Subscription)stripeEvent.Data.Object;
        var userId = await ResolveUserId(stripeEvent, subscription.Metadata?.GetValueOrDefault("userId"), subscription.CustomerId);
        if (userId is null)
        {
            return true;
        }

        if (!TryResolveTierFromSubscription(subscription, out var newTier, out var newInterval))
        {
            logger.LogError("Stripe event {EventId}: subscription {SubscriptionId} has no item matching a configured tier price",
                stripeEvent.Id, subscription.Id);
            return true;
        }

        var user = await userRepository.Get(userId);
        if (user is null)
        {
            logger.LogError("Stripe event {EventId}: user {UserId} not found", stripeEvent.Id, userId);
            return true;
        }

        // A monthly→annual switch is detected from previous_attributes (Stripe's
        // authoritative "what changed" record), not from our stored BillingInterval —
        // this event can race the signup invoice, and a stored-state comparison would
        // read a fresh annual signup as a switch and double-grant the bonus.
        var switchedToAnnual = newInterval == BillingInterval.Year && WasOnMonthlyPrice(stripeEvent);

        if (newTier > user.Role)
        {
            var newGrant = catalog.GetMonthlyGrant(newTier);
            var creditsDelta = Math.Max(newGrant - catalog.GetMonthlyGrant(user.Role), 0);

            var upgraded = await ApplyLedgeredMutation(
                new BillingLedgerRecord
                {
                    UserId = userId,
                    EventKey = EventKey(stripeEvent),
                    Type = BillingLedgerEntryType.UpgradeGrant,
                    AmountCents = 0,
                    Credits = creditsDelta,
                    StripeEventId = stripeEvent.Id,
                    Tier = newTier.ToString(),
                    Description = $"Immediate upgrade {user.Role} -> {newTier}"
                },
                () => userRepository.ApplyUpgradeGrant(userId, newTier, newGrant, creditsDelta, newInterval));

            if (!upgraded)
            {
                return false;
            }

            // Upgrades within annual get no bonus (the year commitment was already
            // rewarded); an upgrade that simultaneously switched monthly→annual does.
            return !switchedToAnnual
                || await GrantAnnualBonus(stripeEvent, userId, newTier, invoiceId: null, "Annual bonus (monthly -> annual switch with upgrade)");
        }

        if (switchedToAnnual)
        {
            // Same-tier (or scheduled-downgrade) interval switch: role and monthly credits
            // are untouched; record the interval and grant the commitment bonus. The SET is
            // idempotent, so it runs outside the ledger guard.
            if (!await userRepository.SetBillingInterval(userId, BillingInterval.Year))
            {
                return false;
            }

            return await GrantAnnualBonus(stripeEvent, userId, newTier, invoiceId: null, "Annual bonus (monthly -> annual switch)");
        }

        // Downgrades are scheduled Stripe-side and take effect when the period-end
        // renewal invoice lands; nothing to do now.
        logger.LogDebug("Subscription update for user {UserId} is not an upgrade or annual switch ({Current} -> {New}); ignoring",
            userId, user.Role, newTier);
        return true;
    }

    private async Task<bool> IsDowngradeFor(string userId, UserRole invoicedTier)
    {
        var user = await userRepository.Get(userId);
        return user is not null && invoicedTier < user.Role;
    }

    /// <summary>
    /// Grants the annual-commitment bonus: one month's grant ADDed to PurchasedCredits,
    /// guarded by an "annual_bonus" ledger row. The "#bonus" EventKey suffix keeps the row
    /// idempotent independently of any other row the same Stripe event wrote.
    /// </summary>
    private async Task<bool> GrantAnnualBonus(Event stripeEvent, string userId, UserRole tier, string invoiceId, string description)
    {
        var bonus = catalog.GetMonthlyGrant(tier);

        return await ApplyLedgeredMutation(
            new BillingLedgerRecord
            {
                UserId = userId,
                EventKey = $"{EventKey(stripeEvent)}#bonus",
                Type = BillingLedgerEntryType.AnnualBonus,
                AmountCents = 0,
                Credits = bonus,
                StripeEventId = stripeEvent.Id,
                StripeInvoiceId = invoiceId,
                Tier = tier.ToString(),
                Description = description
            },
            () => userRepository.AddPurchasedCredits(userId, bonus));
    }

    /// <summary>
    /// True when the subscription.updated event's previous_attributes show the subscription
    /// was on a configured monthly tier price before this update. Shape verified against a
    /// real test-mode capture (API 2025-06-30.basil): the pre-switch price sits at
    /// items.data[*].price.id (and legacy plan mirrors at items.data[*].plan.id and a
    /// top-level "plan" for single-item subscriptions).
    /// </summary>
    private bool WasOnMonthlyPrice(Event stripeEvent)
    {
        if (stripeEvent.Data?.PreviousAttributes is not JObject previous)
        {
            return false;
        }

        var candidates = new List<string> { previous["plan"]?["id"]?.Value<string>() };
        if (previous["items"]?["data"] is JArray items)
        {
            foreach (var item in items)
            {
                candidates.Add(item?["price"]?["id"]?.Value<string>());
                candidates.Add(item?["plan"]?["id"]?.Value<string>());
            }
        }

        return candidates.Any(priceId =>
            catalog.TryResolveTierFromPrice(priceId, out _, out var interval) && interval == BillingInterval.Month);
    }

    private async Task<bool> HandleSubscriptionDeleted(Event stripeEvent)
    {
        var subscription = (Subscription)stripeEvent.Data.Object;
        var userId = await ResolveUserId(stripeEvent, subscription.Metadata?.GetValueOrDefault("userId"), subscription.CustomerId);
        if (userId is null)
        {
            return true;
        }

        return await userRepository.CancelSubscription(userId, catalog.GetMonthlyGrant(UserRole.Free));
    }

    private async Task<bool> HandleInvoicePaymentFailed(Event stripeEvent)
    {
        var invoice = (Invoice)stripeEvent.Data.Object;
        var userId = await ResolveUserId(stripeEvent, invoice.Parent?.SubscriptionDetails?.Metadata?.GetValueOrDefault("userId"), invoice.CustomerId);
        if (userId is null)
        {
            return true;
        }

        // During dunning the user keeps their role and remaining credits; the missing refill
        // is automatic because refills ride invoice.paid.
        return await userRepository.SetSubscriptionStatus(userId, "past_due");
    }

    private async Task<bool> HandleChargeRefunded(Event stripeEvent)
    {
        var charge = (Charge)stripeEvent.Data.Object;
        var userId = await ResolveUserId(stripeEvent, charge.Metadata?.GetValueOrDefault("userId"), charge.CustomerId);
        if (userId is null)
        {
            return true;
        }

        // Credit clawback is a manual operator decision; the ledger row is the audit trail.
        // AmountRefunded is cumulative across partial refunds of the same charge.
        await ledger.TryAppend(new BillingLedgerRecord
        {
            UserId = userId,
            EventKey = EventKey(stripeEvent),
            Type = BillingLedgerEntryType.Refund,
            AmountCents = -charge.AmountRefunded,
            Credits = 0,
            StripeEventId = stripeEvent.Id,
            StripePaymentIntentId = charge.PaymentIntentId,
            Description = $"Refund of charge {charge.Id} (cumulative refunded amount)"
        });

        return true;
    }

    /// <summary>
    /// Appends the ledger row, then applies the mutation. If the mutation fails, the row is
    /// rolled back before signalling a retry — otherwise Stripe's redelivery would collide
    /// with the orphaned ledger key, read as "already applied", and the grant would be
    /// permanently lost. An append collision without a rollback means the mutation already
    /// succeeded, so redelivery stays a no-op.
    /// </summary>
    private async Task<bool> ApplyLedgeredMutation(BillingLedgerRecord entry, Func<Task<bool>> mutation)
    {
        if (!await ledger.TryAppend(entry))
        {
            return true;
        }

        if (await mutation())
        {
            return true;
        }

        logger.LogError(
            "Mutation for ledger entry {EventKey} (user {UserId}, {Type}) failed; rolling back for redelivery",
            entry.EventKey, entry.UserId, entry.Type);
        await ledger.Remove(entry.UserId, entry.EventKey);
        return false;
    }

    private async Task<string> ResolveUserId(Event stripeEvent, string metadataUserId, string customerId)
    {
        if (!string.IsNullOrEmpty(metadataUserId))
        {
            return metadataUserId;
        }

        if (!string.IsNullOrEmpty(customerId))
        {
            var customer = await stripeGateway.GetCustomer(customerId);
            var userId = customer?.Metadata?.GetValueOrDefault("userId");
            if (!string.IsNullOrEmpty(userId))
            {
                return userId;
            }
        }

        logger.LogError("Stripe event {EventId} ({EventType}) could not be resolved to a user (customer {CustomerId})",
            stripeEvent.Id, stripeEvent.Type, customerId);
        return null;
    }

    private bool TryResolveTierFromInvoice(Invoice invoice, out UserRole tier, out string interval)
    {
        tier = default;
        interval = null;

        foreach (var line in invoice.Lines?.Data ?? [])
        {
            if (catalog.TryResolveTierFromPrice(line.Pricing?.PriceDetails?.PriceId, out tier, out interval))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryResolveTierFromSubscription(Subscription subscription, out UserRole tier, out string interval)
    {
        tier = default;
        interval = null;

        foreach (var item in subscription.Items?.Data ?? [])
        {
            if (catalog.TryResolveTierFromPrice(item.Price?.Id, out tier, out interval))
            {
                return true;
            }
        }

        return false;
    }

    private static string EventKey(Event stripeEvent)
    {
        return $"{stripeEvent.Created.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}#{stripeEvent.Id}";
    }
}
