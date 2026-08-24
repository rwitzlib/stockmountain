using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Records;
using MarketViewer.Core.Services;
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

        var appended = await ledger.TryAppend(new BillingLedgerRecord
        {
            UserId = userId,
            EventKey = EventKey(stripeEvent),
            Type = BillingLedgerEntryType.TopupPurchase,
            AmountCents = session.AmountTotal ?? 0,
            Credits = credits,
            StripeEventId = stripeEvent.Id,
            StripePaymentIntentId = session.PaymentIntentId,
            Description = $"Credit pack {packId}"
        });

        if (!appended)
        {
            return true;
        }

        return await userRepository.AddPurchasedCredits(userId, credits);
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

        if (!TryResolveTierFromInvoice(invoice, out var tier))
        {
            logger.LogError("Stripe event {EventId}: invoice {InvoiceId} has no line matching a configured tier price",
                stripeEvent.Id, invoice.Id);
            return true;
        }

        // An upgrade's prorated invoice (billing_reason subscription_update) is money only —
        // the credit delta was already granted by customer.subscription.updated.
        var isUpgradeProration = invoice.BillingReason == "subscription_update";
        var grant = catalog.GetMonthlyGrant(tier);

        var appended = await ledger.TryAppend(new BillingLedgerRecord
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
        });

        if (!appended || isUpgradeProration)
        {
            return true;
        }

        return await userRepository.ApplySubscriptionGrant(userId, tier, grant);
    }

    private async Task<bool> HandleSubscriptionUpdated(Event stripeEvent)
    {
        var subscription = (Subscription)stripeEvent.Data.Object;
        var userId = await ResolveUserId(stripeEvent, subscription.Metadata?.GetValueOrDefault("userId"), subscription.CustomerId);
        if (userId is null)
        {
            return true;
        }

        if (!TryResolveTierFromSubscription(subscription, out var newTier))
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

        if (newTier <= user.Role)
        {
            // Downgrades are scheduled Stripe-side and take effect when the period-end
            // renewal invoice lands; nothing to do now.
            logger.LogDebug("Subscription update for user {UserId} is not an upgrade ({Current} -> {New}); ignoring",
                userId, user.Role, newTier);
            return true;
        }

        var newGrant = catalog.GetMonthlyGrant(newTier);
        var creditsDelta = Math.Max(newGrant - catalog.GetMonthlyGrant(user.Role), 0);

        var appended = await ledger.TryAppend(new BillingLedgerRecord
        {
            UserId = userId,
            EventKey = EventKey(stripeEvent),
            Type = BillingLedgerEntryType.UpgradeGrant,
            AmountCents = 0,
            Credits = creditsDelta,
            StripeEventId = stripeEvent.Id,
            Tier = newTier.ToString(),
            Description = $"Immediate upgrade {user.Role} -> {newTier}"
        });

        if (!appended)
        {
            return true;
        }

        return await userRepository.ApplyUpgradeGrant(userId, newTier, newGrant, creditsDelta);
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

    private bool TryResolveTierFromInvoice(Invoice invoice, out UserRole tier)
    {
        tier = default;

        foreach (var line in invoice.Lines?.Data ?? [])
        {
            if (catalog.TryResolveTierFromPrice(line.Pricing?.PriceDetails?.PriceId, out tier))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryResolveTierFromSubscription(Subscription subscription, out UserRole tier)
    {
        tier = default;

        foreach (var item in subscription.Items?.Data ?? [])
        {
            if (catalog.TryResolveTierFromPrice(item.Price?.Id, out tier))
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
