using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Records;

namespace MarketViewer.Core.Services;

public interface IUserRepository
{
    Task<bool> Put(UserRecord user);
    Task<bool> Provision(UserRecord user);
    Task<UserRecord> Get(string id);
    Task<bool> TryDebitCredits(string id, float credits);

    /// <summary>Links the Stripe customer created at first checkout to the user record.</summary>
    Task<bool> SetStripeCustomerId(string id, string stripeCustomerId);

    /// <summary>
    /// Applies a paid subscription invoice: sets the role and resets the monthly allowance to
    /// the tier grant (monthly credits do not roll over). Marks the subscription active and
    /// records the billing interval ("month"/"year") of the paying price.
    /// </summary>
    Task<bool> ApplySubscriptionGrant(string id, UserRole role, float monthlyGrant, string billingInterval);

    /// <summary>
    /// Applies an immediate mid-cycle upgrade: sets the new role and MaxCredits, and adds the
    /// grant difference on top of the remaining monthly balance. Also records the billing
    /// interval of the new price.
    /// </summary>
    Task<bool> ApplyUpgradeGrant(string id, UserRole role, float monthlyGrant, float creditsDelta, string billingInterval);

    /// <summary>Adds never-expiring top-up credits from a credit-pack purchase.</summary>
    Task<bool> AddPurchasedCredits(string id, float credits);

    /// <summary>Display-only subscription status ("active", "past_due", "canceled").</summary>
    Task<bool> SetSubscriptionStatus(string id, string status);

    /// <summary>Records the subscription's billing interval ("month"/"year"); idempotent SET.</summary>
    Task<bool> SetBillingInterval(string id, string billingInterval);

    /// <summary>
    /// Drops the user to the Free tier after subscription cancellation: monthly credits are
    /// clamped to the Free grant, purchased credits are untouched.
    /// </summary>
    Task<bool> CancelSubscription(string id, float freeGrant);
}
