using MarketViewer.Api.Config;
using MarketViewer.Contracts.Enums;

namespace MarketViewer.Api.Services.Billing;

/// <summary>
/// Immutable lookup over the billing config: tier grants (Tiers section), credit packs
/// (Packs section), and Stripe Price ids (Stripe:Prices). Price keys are tier names for
/// subscriptions ("Pro", "Premium"), tier names with an "Annual" suffix for the yearly
/// prices ("ProAnnual", "PremiumAnnual"), and pack ids for one-time packs ("PackSmall",
/// "PackLarge"), which lets webhooks map a Stripe price back to a tier and interval.
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
        return TryResolveTierFromPrice(priceId, out tier, out _);
    }

    public bool TryResolveTierFromPrice(string priceId, out UserRole tier, out string interval)
    {
        tier = default;
        interval = null;

        if (string.IsNullOrEmpty(priceId))
        {
            return false;
        }

        foreach (var (key, value) in prices)
        {
            if (value == priceId && TryResolveTierFromKey(key, out tier, out interval))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when the price id is a configured "{Tier}Annual" yearly price.</summary>
    public bool IsAnnualPrice(string priceId)
    {
        return TryResolveTierFromPrice(priceId, out _, out var interval) && interval == BillingInterval.Year;
    }

    /// <summary>
    /// Resolves a subscription price key ("Pro", "PremiumAnnual", ...) to its tier and
    /// billing interval. Pack keys and unknowns return false. The annual match is the exact
    /// "{Tier}Annual" pattern — a future key like "ProAnnualPromo" must not resolve here.
    /// </summary>
    public static bool TryResolveTierFromKey(string key, out UserRole tier, out string interval)
    {
        tier = default;
        interval = null;

        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (Enum.TryParse(key, out tier))
        {
            interval = BillingInterval.Month;
            return true;
        }

        const string suffix = "Annual";
        if (key.EndsWith(suffix) && Enum.TryParse(key[..^suffix.Length], out tier))
        {
            interval = BillingInterval.Year;
            return true;
        }

        return false;
    }
}

/// <summary>Stripe's recurring-interval strings, also stored on UserRecord.BillingInterval.</summary>
public static class BillingInterval
{
    public const string Month = "month";
    public const string Year = "year";
}
