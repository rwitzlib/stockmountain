using Stripe;

namespace MarketViewer.Api.Services.Billing;

/// <summary>
/// Thin wrapper around the Stripe API calls the billing flow makes over the network,
/// so controllers and the webhook processor stay unit-testable without Stripe traffic.
/// </summary>
public interface IStripeGateway
{
    /// <summary>Creates a Stripe customer tagged with the Clerk user id; returns the customer id.</summary>
    Task<string> CreateCustomer(string userId);

    Task<Customer> GetCustomer(string customerId);

    /// <summary>
    /// True when the customer has a subscription that exists or is in flight on Stripe's
    /// side (anything but canceled/expired). Guards the webhook-lag window where our
    /// stored SubscriptionStatus isn't "active" yet but a paid subscription already
    /// exists — a second subscription checkout would double-charge.
    /// </summary>
    Task<bool> HasLiveSubscription(string customerId);

    /// <summary>Creates an embedded Checkout session and returns its client secret.</summary>
    Task<string> CreateCheckoutSession(CheckoutSessionSpec spec);

    /// <summary>Creates a Customer Portal session and returns its hosted-page URL.</summary>
    Task<string> CreatePortalSession(string customerId, string returnUrl);

    /// <summary>
    /// The customer's live subscription (anything but canceled/expired) with the fields the
    /// in-app plan change needs, or null when there is none.
    /// </summary>
    Task<LiveSubscription> GetLiveSubscription(string customerId);

    /// <summary>Prorated amount Stripe would invoice today for switching the subscription to the price.</summary>
    Task<ProrationPreview> PreviewImmediateChange(LiveSubscription subscription, string priceId);

    /// <summary>
    /// Switches the subscription to the price now, invoicing the proration immediately. When
    /// the payment needs customer action the subscription is left unchanged as a pending
    /// update and the result carries the hosted invoice URL to complete it.
    /// </summary>
    Task<ImmediateChangeResult> ChangePlanNow(LiveSubscription subscription, string priceId);

    /// <summary>
    /// Schedules the switch to the price for the end of the current period (no proration),
    /// replacing any previously scheduled change. Returns when it takes effect.
    /// </summary>
    Task<DateTime> SchedulePlanChangeAtPeriodEnd(LiveSubscription subscription, string priceId, string priceInterval);

    /// <summary>Drops a scheduled period-end change; the subscription continues on its current price.</summary>
    Task ReleaseScheduledChange(LiveSubscription subscription);
}

/// <summary>Stripe subscription projected to what plan changes need; built by the gateway.</summary>
public class LiveSubscription
{
    public string Id { get; set; }
    public string CustomerId { get; set; }

    /// <summary>The single subscription item carrying the tier price.</summary>
    public string ItemId { get; set; }
    public string PriceId { get; set; }

    /// <summary>Stripe status: "active", "past_due", "trialing", "unpaid", "incomplete", ...</summary>
    public string Status { get; set; }
    public DateTime CurrentPeriodEnd { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];

    /// <summary>Subscription schedule managing this subscription, when a period-end change is pending.</summary>
    public string ScheduleId { get; set; }
    public string ScheduledPriceId { get; set; }
    public DateTime? ScheduledStartsAt { get; set; }
}

public class ProrationPreview
{
    public long AmountDueCents { get; set; }
    public string Currency { get; set; }
}

public class ImmediateChangeResult
{
    /// <summary>True when Stripe applied the new price; false when the payment is pending customer action.</summary>
    public bool Applied { get; set; }

    /// <summary>Hosted invoice URL to complete an incomplete prorated payment; null when applied.</summary>
    public string PaymentUrl { get; set; }
}

public class CheckoutSessionSpec
{
    public string UserId { get; set; }
    public string CustomerId { get; set; }
    public string PriceId { get; set; }
    public bool IsSubscription { get; set; }

    /// <summary>Pack id ("PackSmall"/"PackLarge") for one-time purchases; null for subscriptions.</summary>
    public string PackId { get; set; }
}
