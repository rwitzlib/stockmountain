using MarketViewer.Api.Config;
using MarketViewer.Contracts.Enums;

namespace MarketViewer.Api.Services.Billing;

/// <summary>
/// Immutable lookup over the billing config: tier grants (Tiers section), credit packs
/// (Packs section), and Stripe Price ids (Stripe:Prices). Price keys are tier names for
/// subscriptions ("Pro", "Premium") and pack ids for one-time packs ("PackSmall", "PackLarge"),
/// which lets webhooks map a Stripe price back to a tier.
/// </summary>
public class BillingCatalog(
    Dictionary<UserRole, float> tierGrants,
    Dictionary<string, float> packCredits,
    Dictionary<string, string> prices)
{
    public static BillingCatalog FromConfiguration(IConfiguration configuration)
    {
        var tiers = configuration.GetSection("Tiers").Get<Dictionary<string, TierConfig>>() ?? [];
        var packs = configuration.GetSection("Packs").Get<Dictionary<string, PackConfig>>() ?? [];
        var stripePrices = configuration.GetSection("Stripe:Prices").Get<Dictionary<string, string>>() ?? [];

        return new BillingCatalog(
            tiers.ToDictionary(kvp => Enum.Parse<UserRole>(kvp.Key), kvp => kvp.Value.MonthlyCredits),
            packs.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Credits),
            stripePrices);
    }

    public float GetMonthlyGrant(UserRole tier)
    {
        return tierGrants.GetValueOrDefault(tier, 0);
    }

    public bool TryGetPackCredits(string packId, out float credits)
    {
        credits = 0;
        return !string.IsNullOrEmpty(packId) && packCredits.TryGetValue(packId, out credits);
    }

    public bool TryGetPriceId(string key, out string priceId)
    {
        priceId = null;
        return !string.IsNullOrEmpty(key)
            && prices.TryGetValue(key, out priceId)
            && !string.IsNullOrEmpty(priceId);
    }

    public bool TryResolveTierFromPrice(string priceId, out UserRole tier)
    {
        tier = default;

        if (string.IsNullOrEmpty(priceId))
        {
            return false;
        }

        foreach (var (key, value) in prices)
        {
            if (value == priceId && Enum.TryParse(key, out tier))
            {
                return true;
            }
        }

        return false;
    }
}
