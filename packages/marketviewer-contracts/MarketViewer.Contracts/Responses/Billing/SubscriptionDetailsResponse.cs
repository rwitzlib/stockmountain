using MarketViewer.Contracts.Enums;

namespace MarketViewer.Contracts.Responses.Billing;

/// <summary>
/// Live view of the user's Stripe subscription, read from Stripe on demand (the summary
/// endpoint stays a cheap user-store read). Drives the in-app plan-change controls.
/// </summary>
public class SubscriptionDetailsResponse
{
    /// <summary>False when the customer has no live subscription; every other field is then unset.</summary>
    public bool HasSubscription { get; set; }

    public UserRole Tier { get; set; }

    /// <summary>"month" or "year".</summary>
    public string Interval { get; set; }

    /// <summary>Stripe subscription status ("active", "past_due", ...).</summary>
    public string Status { get; set; }

    public DateTime? CurrentPeriodEnd { get; set; }

    /// <summary>Set when the user cancelled in the portal; plan changes are refused until reactivated.</summary>
    public bool CancelAtPeriodEnd { get; set; }

    /// <summary>A period-end plan change scheduled through the API, if any.</summary>
    public PendingPlanChange PendingChange { get; set; }
}

public class PendingPlanChange
{
    public UserRole Tier { get; set; }
    public string Interval { get; set; }
    public DateTime EffectiveAt { get; set; }
}
