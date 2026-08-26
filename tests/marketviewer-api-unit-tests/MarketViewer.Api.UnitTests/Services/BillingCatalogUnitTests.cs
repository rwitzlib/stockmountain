using FluentAssertions;
using MarketViewer.Api.Services.Billing;
using MarketViewer.Contracts.Enums;
using Xunit;

namespace MarketViewer.Api.UnitTests.Services;

public class BillingCatalogUnitTests
{
    private readonly BillingCatalog _catalog = new(
        new Dictionary<UserRole, float> { { UserRole.Free, 100 }, { UserRole.Pro, 1000 }, { UserRole.Premium, 5000 } },
        new Dictionary<string, float> { { "PackSmall", 250 }, { "PackLarge", 1000 } },
        new Dictionary<string, string>
        {
            { "Pro", "price_pro" },
            { "Premium", "price_premium" },
            { "ProAnnual", "price_pro_annual" },
            { "PremiumAnnual", "price_premium_annual" },
            { "PackSmall", "price_pack_small" },
            { "PackLarge", "price_pack_large" }
        });

    [Theory]
    [InlineData("price_pro", UserRole.Pro, "month")]
    [InlineData("price_premium", UserRole.Premium, "month")]
    [InlineData("price_pro_annual", UserRole.Pro, "year")]
    [InlineData("price_premium_annual", UserRole.Premium, "year")]
    public void TryResolveTierFromPrice_SubscriptionPrices_ResolveTierAndInterval(string priceId, UserRole expectedTier, string expectedInterval)
    {
        _catalog.TryResolveTierFromPrice(priceId, out var tier, out var interval).Should().BeTrue();
        tier.Should().Be(expectedTier);
        interval.Should().Be(expectedInterval);
    }

    [Theory]
    [InlineData("price_pack_small")]
    [InlineData("price_pack_large")]
    [InlineData("price_unknown")]
    [InlineData("")]
    [InlineData(null)]
    public void TryResolveTierFromPrice_PackAndUnknownPrices_DoNotResolve(string priceId)
    {
        _catalog.TryResolveTierFromPrice(priceId, out _, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("price_pro_annual", true)]
    [InlineData("price_premium_annual", true)]
    [InlineData("price_pro", false)]
    [InlineData("price_pack_small", false)]
    [InlineData("price_unknown", false)]
    public void IsAnnualPrice_OnlyForConfiguredAnnualTierPrices(string priceId, bool expected)
    {
        _catalog.IsAnnualPrice(priceId).Should().Be(expected);
    }

    [Theory]
    [InlineData("Pro", UserRole.Pro, "month")]
    [InlineData("Premium", UserRole.Premium, "month")]
    [InlineData("Free", UserRole.Free, "month")]
    [InlineData("ProAnnual", UserRole.Pro, "year")]
    [InlineData("PremiumAnnual", UserRole.Premium, "year")]
    [InlineData("FreeAnnual", UserRole.Free, "year")]
    public void TryResolveTierFromKey_TierKeys_Resolve(string key, UserRole expectedTier, string expectedInterval)
    {
        BillingCatalog.TryResolveTierFromKey(key, out var tier, out var interval).Should().BeTrue();
        tier.Should().Be(expectedTier);
        interval.Should().Be(expectedInterval);
    }

    [Theory]
    [InlineData("PackSmall")]
    [InlineData("PackLarge")]
    [InlineData("ProAnnualPromo")] // suffix match must be the exact {Tier}Annual pattern
    [InlineData("AnnualPromo")]
    [InlineData("PromoAnnual")]
    [InlineData("Annual")]
    [InlineData("Gold")]
    [InlineData("999")] // Enum.TryParse accepts numeric strings; IsDefined must reject them
    [InlineData("999Annual")]
    [InlineData("")]
    [InlineData(null)]
    public void TryResolveTierFromKey_NonTierKeys_DoNotResolve(string key)
    {
        BillingCatalog.TryResolveTierFromKey(key, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetPriceId_AnnualKeys_ResolveConfiguredPrices()
    {
        _catalog.TryGetPriceId("ProAnnual", out var priceId).Should().BeTrue();
        priceId.Should().Be("price_pro_annual");
    }
}
