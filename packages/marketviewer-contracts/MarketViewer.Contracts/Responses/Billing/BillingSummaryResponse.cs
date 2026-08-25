using MarketViewer.Contracts.Enums;

namespace MarketViewer.Contracts.Responses.Billing;

public class BillingSummaryResponse
{
    public UserRole Tier { get; set; }
    public float Credits { get; set; }
    public float MaxCredits { get; set; }
    public float PurchasedCredits { get; set; }

    /// <summary>"active", "past_due", "canceled", or "none". Display only; Tier is the enforcement field.</summary>
    public string SubscriptionStatus { get; set; }

    /// <summary>Whether a Stripe customer exists — the portal-session endpoint 400s without one.</summary>
    public bool HasBillingAccount { get; set; }
}
