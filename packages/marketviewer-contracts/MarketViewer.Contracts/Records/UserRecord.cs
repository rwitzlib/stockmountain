using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Enums.Strategy;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Records;

[ExcludeFromCodeCoverage]
public class UserRecord
{
    public string Id { get; set; }
    public string AvatarUrl { get; set; }
    /// <summary>Monthly credit balance; reset to the tier grant on each refill.</summary>
    public float Credits { get; set; }
    public float MaxCredits { get; set; }
    /// <summary>Top-up credits bought as one-time packs; never expire, spent after Credits.</summary>
    public float PurchasedCredits { get; set; }
    public bool IsPublic { get; set; }
    public UserRole Role { get; set; }
    public bool IsAdmin { get; set; }
    public string StripeCustomerId { get; set; }
    /// <summary>Display-only Stripe subscription status (active/past_due/canceled); Role stays the enforcement field.</summary>
    public string SubscriptionStatus { get; set; }
    /// <summary>
    /// Billing interval of the active subscription ("month"/"year"). Absent on legacy and
    /// non-subscribed records — only "year" carries meaning (annual users refill via the
    /// monthly Lambda instead of invoice.paid).
    /// </summary>
    public string BillingInterval { get; set; }
    public Dictionary<IntegrationType, string> Tokens { get; set; }
}
