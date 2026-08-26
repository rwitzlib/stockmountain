using FluentAssertions;
using MarketViewer.Api.Services.Billing;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Records;
using MarketViewer.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stripe;
using Xunit;

namespace MarketViewer.Api.UnitTests.Services;

/// <summary>
/// Exercises the webhook processor against events constructed from JSON fixtures with the
/// Stripe SDK's own parser (no network) — the phase-2 "integration tests with Stripe's test
/// fixtures" from plan 16.
/// </summary>
public class StripeWebhookProcessorUnitTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IBillingLedgerRepository> _ledger = new();
    private readonly Mock<IStripeGateway> _gateway = new();
    private readonly StripeWebhookProcessor _processor;

    public StripeWebhookProcessorUnitTests()
    {
        var catalog = new BillingCatalog(
            new Dictionary<UserRole, float>
            {
                { UserRole.Free, 100 },
                { UserRole.Pro, 1000 },
                { UserRole.Premium, 5000 }
            },
            new Dictionary<string, float>
            {
                { "PackSmall", 250 },
                { "PackLarge", 1000 }
            },
            new Dictionary<string, string>
            {
                { "Pro", "price_pro" },
                { "Premium", "price_premium" },
                { "ProAnnual", "price_pro_annual" },
                { "PremiumAnnual", "price_premium_annual" },
                { "PackSmall", "price_pack_small" },
                { "PackLarge", "price_pack_large" }
            });

        _processor = new StripeWebhookProcessor(
            _users.Object,
            _ledger.Object,
            _gateway.Object,
            catalog,
            NullLogger<StripeWebhookProcessor>.Instance);

        _ledger.Setup(l => l.TryAppend(It.IsAny<BillingLedgerRecord>())).ReturnsAsync(true);
    }

    private static Event ParseEvent(string json)
    {
        return EventUtility.ParseEvent(json, throwOnApiVersionMismatch: false);
    }

    private UserRecord SetupUser(UserRole role, string stripeCustomerId = null, float credits = 50)
    {
        var user = new UserRecord
        {
            Id = "user-1",
            Role = role,
            Credits = credits,
            MaxCredits = 100,
            PurchasedCredits = 0,
            StripeCustomerId = stripeCustomerId
        };
        _users.Setup(u => u.Get("user-1")).ReturnsAsync(user);
        return user;
    }

    private static string CheckoutSessionJson(string mode, string metadata = @"{ ""userId"": ""user-1"", ""pack"": ""PackSmall"" }")
    {
        return $$"""
        {
          "id": "evt_checkout_1",
          "object": "event",
          "api_version": "2026-01-01",
          "created": 1756036800,
          "livemode": false,
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "id": "cs_1",
              "object": "checkout.session",
              "mode": "{{mode}}",
              "client_reference_id": "user-1",
              "customer": "cus_1",
              "amount_total": 1000,
              "payment_intent": "pi_1",
              "metadata": {{metadata}}
            }
          }
        }
        """;
    }

    private static string InvoiceJson(string type, string billingReason, string priceId = "price_pro", bool withMetadata = true)
    {
        var metadata = withMetadata ? @"{ ""userId"": ""user-1"" }" : "{ }";
        return $$"""
        {
          "id": "evt_invoice_1",
          "object": "event",
          "api_version": "2026-01-01",
          "created": 1756036800,
          "livemode": false,
          "type": "{{type}}",
          "data": {
            "object": {
              "id": "in_1",
              "object": "invoice",
              "customer": "cus_1",
              "amount_paid": 2900,
              "billing_reason": "{{billingReason}}",
              "parent": {
                "type": "subscription_details",
                "subscription_details": { "subscription": "sub_1", "metadata": {{metadata}} }
              },
              "lines": {
                "object": "list",
                "data": [
                  {
                    "id": "il_1",
                    "object": "line_item",
                    "pricing": {
                      "type": "price_details",
                      "price_details": { "price": "{{priceId}}", "product": "prod_1" },
                      "unit_amount_decimal": "2900"
                    }
                  }
                ],
                "has_more": false,
                "url": "/v1/invoices/in_1/lines"
              }
            }
          }
        }
        """;
    }

    private static string SubscriptionJson(string type, string priceId, string previousPriceId = null, string previousInterval = "month")
    {
        // The previous_attributes shape mirrors a real test-mode capture of a
        // monthly→annual price switch (API 2025-06-30.basil): the old price appears at
        // items.data[*].price/plan and as a top-level "plan" mirror.
        var previousAttributes = previousPriceId is null
            ? string.Empty
            : $$"""
            ,
            "previous_attributes": {
              "billing_cycle_anchor": 1756036000,
              "items": {
                "data": [
                  {
                    "id": "si_1",
                    "object": "subscription_item",
                    "plan": { "id": "{{previousPriceId}}", "object": "plan", "interval": "{{previousInterval}}", "product": "prod_1" },
                    "price": { "id": "{{previousPriceId}}", "object": "price", "recurring": { "interval": "{{previousInterval}}" } },
                    "quantity": 1,
                    "subscription": "sub_1"
                  }
                ]
              },
              "latest_invoice": "in_0",
              "plan": { "id": "{{previousPriceId}}", "interval": "{{previousInterval}}" }
            }
            """;

        return $$"""
        {
          "id": "evt_sub_1",
          "object": "event",
          "api_version": "2026-01-01",
          "created": 1756036800,
          "livemode": false,
          "type": "{{type}}",
          "data": {
            "object": {
              "id": "sub_1",
              "object": "subscription",
              "customer": "cus_1",
              "status": "active",
              "metadata": { "userId": "user-1" },
              "items": {
                "object": "list",
                "data": [
                  {
                    "id": "si_1",
                    "object": "subscription_item",
                    "price": { "id": "{{priceId}}", "object": "price" }
                  }
                ],
                "has_more": false,
                "url": "/v1/subscription_items?subscription=sub_1"
              }
            }{{previousAttributes}}
          }
        }
        """;
    }

    [Fact]
    public async Task PackPurchase_AppendsLedgerAndAddsPurchasedCredits()
    {
        SetupUser(UserRole.Free, stripeCustomerId: "cus_1");
        _users.Setup(u => u.AddPurchasedCredits("user-1", 250)).ReturnsAsync(true);
        BillingLedgerRecord entry = null;
        _ledger.Setup(l => l.TryAppend(It.IsAny<BillingLedgerRecord>()))
            .Callback<BillingLedgerRecord>(e => entry = e)
            .ReturnsAsync(true);

        var result = await _processor.Process(ParseEvent(CheckoutSessionJson("payment")));

        result.Should().BeTrue();
        _users.Verify(u => u.AddPurchasedCredits("user-1", 250), Times.Once);
        _ledger.Verify(l => l.Remove(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        entry.Type.Should().Be(BillingLedgerEntryType.TopupPurchase);
        entry.UserId.Should().Be("user-1");
        entry.AmountCents.Should().Be(1000);
        entry.Credits.Should().Be(250);
        entry.StripeEventId.Should().Be("evt_checkout_1");
        entry.StripePaymentIntentId.Should().Be("pi_1");
        entry.EventKey.Should().EndWith("#evt_checkout_1");
    }

    [Fact]
    public async Task PackPurchase_Redelivery_DoesNotGrantTwice()
    {
        SetupUser(UserRole.Free, stripeCustomerId: "cus_1");
        _ledger.Setup(l => l.TryAppend(It.IsAny<BillingLedgerRecord>())).ReturnsAsync(false);

        var result = await _processor.Process(ParseEvent(CheckoutSessionJson("payment")));

        result.Should().BeTrue();
        _users.Verify(u => u.AddPurchasedCredits(It.IsAny<string>(), It.IsAny<float>()), Times.Never);
    }

    [Fact]
    public async Task SubscriptionCheckout_LinksCustomerWithoutGranting()
    {
        SetupUser(UserRole.Free, stripeCustomerId: null);
        _users.Setup(u => u.SetStripeCustomerId("user-1", "cus_1")).ReturnsAsync(true);

        var result = await _processor.Process(ParseEvent(CheckoutSessionJson("subscription", @"{ ""userId"": ""user-1"" }")));

        result.Should().BeTrue();
        _users.Verify(u => u.SetStripeCustomerId("user-1", "cus_1"), Times.Once);
        _ledger.Verify(l => l.TryAppend(It.IsAny<BillingLedgerRecord>()), Times.Never);
        _users.Verify(u => u.ApplySubscriptionGrant(It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<float>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InvoicePaid_RenewalGrantsMonthlyCredits()
    {
        _users.Setup(u => u.ApplySubscriptionGrant("user-1", UserRole.Pro, 1000, "month")).ReturnsAsync(true);
        BillingLedgerRecord entry = null;
        _ledger.Setup(l => l.TryAppend(It.IsAny<BillingLedgerRecord>()))
            .Callback<BillingLedgerRecord>(e => entry = e)
            .ReturnsAsync(true);

        var result = await _processor.Process(ParseEvent(InvoiceJson("invoice.paid", "subscription_cycle")));

        result.Should().BeTrue();
        _users.Verify(u => u.ApplySubscriptionGrant("user-1", UserRole.Pro, 1000, "month"), Times.Once);
        _users.Verify(u => u.AddPurchasedCredits(It.IsAny<string>(), It.IsAny<float>()), Times.Never);
        entry.Type.Should().Be(BillingLedgerEntryType.SubscriptionPayment);
        entry.AmountCents.Should().Be(2900);
        entry.Credits.Should().Be(1000);
        entry.StripeInvoiceId.Should().Be("in_1");
        entry.Tier.Should().Be("Pro");
    }

    [Theory]
    [InlineData("subscription_create")]
    [InlineData("subscription_cycle")]
    public async Task InvoicePaid_Annual_GrantsMonthlyCreditsAndBonus(string billingReason)
    {
        // Annual signup and every annual renewal: the normal grant (with the year interval
        // recorded) plus one month's grant into the never-expiring purchased balance.
        _users.Setup(u => u.ApplySubscriptionGrant("user-1", UserRole.Pro, 1000, "year")).ReturnsAsync(true);
        _users.Setup(u => u.AddPurchasedCredits("user-1", 1000)).ReturnsAsync(true);
        var entries = new List<BillingLedgerRecord>();
        _ledger.Setup(l => l.TryAppend(It.IsAny<BillingLedgerRecord>()))
            .Callback<BillingLedgerRecord>(entries.Add)
            .ReturnsAsync(true);

        var result = await _processor.Process(ParseEvent(InvoiceJson("invoice.paid", billingReason, "price_pro_annual")));

        result.Should().BeTrue();
        _users.Verify(u => u.ApplySubscriptionGrant("user-1", UserRole.Pro, 1000, "year"), Times.Once);
        _users.Verify(u => u.AddPurchasedCredits("user-1", 1000), Times.Once);

        entries.Should().HaveCount(2);
        entries[0].Type.Should().Be(BillingLedgerEntryType.SubscriptionPayment);
        entries[0].Credits.Should().Be(1000);
        var bonus = entries[1];
        bonus.Type.Should().Be(BillingLedgerEntryType.AnnualBonus);
        bonus.Credits.Should().Be(1000);
        bonus.AmountCents.Should().Be(0);
        bonus.Tier.Should().Be("Pro");
        // Independent idempotency: the bonus row must not collide with the payment row.
        bonus.EventKey.Should().EndWith("#evt_invoice_1#bonus");
    }

    [Fact]
    public async Task InvoicePaid_AnnualBonusAlreadyGranted_DoesNotGrantTwice()
    {
        // Redelivery after a partial apply: the payment row exists, the bonus row exists —
        // neither mutation may run again.
        _ledger.Setup(l => l.TryAppend(It.IsAny<BillingLedgerRecord>())).ReturnsAsync(false);

        var result = await _processor.Process(ParseEvent(InvoiceJson("invoice.paid", "subscription_create", "price_pro_annual")));

        result.Should().BeTrue();
        _users.Verify(u => u.ApplySubscriptionGrant(It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<float>(), It.IsAny<string>()), Times.Never);
        _users.Verify(u => u.AddPurchasedCredits(It.IsAny<string>(), It.IsAny<float>()), Times.Never);
    }

    [Fact]
    public async Task InvoicePaid_AnnualBonusFails_RollsBackBonusRowAndSignalsRetry()
    {
        _users.Setup(u => u.ApplySubscriptionGrant("user-1", UserRole.Pro, 1000, "year")).ReturnsAsync(true);
        _users.Setup(u => u.AddPurchasedCredits("user-1", 1000)).ReturnsAsync(false);

        var result = await _processor.Process(ParseEvent(InvoiceJson("invoice.paid", "subscription_create", "price_pro_annual")));

        result.Should().BeFalse();
        // Only the bonus row rolls back; the applied payment row must stand so the
        // redelivery no-ops the grant and retries just the bonus.
        _ledger.Verify(l => l.Remove("user-1", It.Is<string>(k => k.EndsWith("#evt_invoice_1#bonus"))), Times.Once);
        _ledger.Verify(l => l.Remove("user-1", It.Is<string>(k => k.EndsWith("#evt_invoice_1"))), Times.Never);
    }

    [Theory]
    [InlineData("price_premium")]
    [InlineData("price_premium_annual")]
    public async Task InvoicePaid_UpgradeProration_IsMoneyOnly(string priceId)
    {
        // Applies to the prorated invoice of a tier upgrade AND of a monthly→annual switch
        // (both billing_reason subscription_update): the credit delta and the switch bonus
        // ride customer.subscription.updated, never this invoice.
        BillingLedgerRecord entry = null;
        _ledger.Setup(l => l.TryAppend(It.IsAny<BillingLedgerRecord>()))
            .Callback<BillingLedgerRecord>(e => entry = e)
            .ReturnsAsync(true);

        var result = await _processor.Process(ParseEvent(InvoiceJson("invoice.paid", "subscription_update", priceId)));

        result.Should().BeTrue();
        _users.Verify(u => u.ApplySubscriptionGrant(It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<float>(), It.IsAny<string>()), Times.Never);
        _users.Verify(u => u.AddPurchasedCredits(It.IsAny<string>(), It.IsAny<float>()), Times.Never);
        entry.Credits.Should().Be(0);
        entry.AmountCents.Should().Be(2900);
    }

    [Fact]
    public async Task InvoicePaid_MissingMetadata_ResolvesUserViaCustomerLookup()
    {
        _gateway.Setup(g => g.GetCustomer("cus_1")).ReturnsAsync(new Customer
        {
            Id = "cus_1",
            Metadata = new Dictionary<string, string> { { "userId", "user-1" } }
        });
        _users.Setup(u => u.ApplySubscriptionGrant("user-1", UserRole.Pro, 1000, "month")).ReturnsAsync(true);

        var result = await _processor.Process(ParseEvent(InvoiceJson("invoice.paid", "subscription_cycle", withMetadata: false)));

        result.Should().BeTrue();
        _users.Verify(u => u.ApplySubscriptionGrant("user-1", UserRole.Pro, 1000, "month"), Times.Once);
    }

    [Fact]
    public async Task InvoicePaid_GrantFails_RollsBackLedgerAndSignalsRetry()
    {
        _users.Setup(u => u.ApplySubscriptionGrant("user-1", UserRole.Pro, 1000, "month")).ReturnsAsync(false);

        var result = await _processor.Process(ParseEvent(InvoiceJson("invoice.paid", "subscription_cycle")));

        result.Should().BeFalse();
        // Without the rollback, Stripe's redelivery would collide with the orphaned ledger
        // key and the grant would be lost forever.
        _ledger.Verify(l => l.Remove("user-1", It.Is<string>(k => k.EndsWith("#evt_invoice_1"))), Times.Once);
    }

    [Fact]
    public async Task PackPurchase_MutationFails_RollsBackLedgerAndSignalsRetry()
    {
        SetupUser(UserRole.Free, stripeCustomerId: "cus_1");
        _users.Setup(u => u.AddPurchasedCredits("user-1", 250)).ReturnsAsync(false);

        var result = await _processor.Process(ParseEvent(CheckoutSessionJson("payment")));

        result.Should().BeFalse();
        _ledger.Verify(l => l.Remove("user-1", It.Is<string>(k => k.EndsWith("#evt_checkout_1"))), Times.Once);
    }

    [Fact]
    public async Task SubscriptionUpdated_Upgrade_GrantsDeltaImmediately()
    {
        SetupUser(UserRole.Pro);
        _users.Setup(u => u.ApplyUpgradeGrant("user-1", UserRole.Premium, 5000, 4000, "month")).ReturnsAsync(true);
        BillingLedgerRecord entry = null;
        _ledger.Setup(l => l.TryAppend(It.IsAny<BillingLedgerRecord>()))
            .Callback<BillingLedgerRecord>(e => entry = e)
            .ReturnsAsync(true);

        var result = await _processor.Process(ParseEvent(SubscriptionJson("customer.subscription.updated", "price_premium")));

        result.Should().BeTrue();
        _users.Verify(u => u.ApplyUpgradeGrant("user-1", UserRole.Premium, 5000, 4000, "month"), Times.Once);
        _users.Verify(u => u.AddPurchasedCredits(It.IsAny<string>(), It.IsAny<float>()), Times.Never);
        entry.Type.Should().Be(BillingLedgerEntryType.UpgradeGrant);
        entry.Credits.Should().Be(4000);
        entry.AmountCents.Should().Be(0);
        entry.Tier.Should().Be("Premium");
    }

    [Fact]
    public async Task SubscriptionUpdated_SameTierMonthlyToAnnualSwitch_GrantsBonusAndSetsInterval()
    {
        // Pro monthly → Pro annual through the portal: role and monthly credits untouched,
        // interval recorded, one month's grant into the purchased balance.
        SetupUser(UserRole.Pro);
        _users.Setup(u => u.SetBillingInterval("user-1", "year")).ReturnsAsync(true);
        _users.Setup(u => u.AddPurchasedCredits("user-1", 1000)).ReturnsAsync(true);
        BillingLedgerRecord entry = null;
        _ledger.Setup(l => l.TryAppend(It.IsAny<BillingLedgerRecord>()))
            .Callback<BillingLedgerRecord>(e => entry = e)
            .ReturnsAsync(true);

        var result = await _processor.Process(ParseEvent(
            SubscriptionJson("customer.subscription.updated", "price_pro_annual", previousPriceId: "price_pro")));

        result.Should().BeTrue();
        _users.Verify(u => u.SetBillingInterval("user-1", "year"), Times.Once);
        _users.Verify(u => u.AddPurchasedCredits("user-1", 1000), Times.Once);
        _users.Verify(u => u.ApplyUpgradeGrant(It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<string>()), Times.Never);
        entry.Type.Should().Be(BillingLedgerEntryType.AnnualBonus);
        entry.Credits.Should().Be(1000);
        entry.Tier.Should().Be("Pro");
        entry.EventKey.Should().EndWith("#evt_sub_1#bonus");
    }

    [Fact]
    public async Task SubscriptionUpdated_SameTierSwitch_Redelivery_DoesNotGrantTwice()
    {
        SetupUser(UserRole.Pro);
        _users.Setup(u => u.SetBillingInterval("user-1", "year")).ReturnsAsync(true);
        _ledger.Setup(l => l.TryAppend(It.IsAny<BillingLedgerRecord>())).ReturnsAsync(false);

        var result = await _processor.Process(ParseEvent(
            SubscriptionJson("customer.subscription.updated", "price_pro_annual", previousPriceId: "price_pro")));

        result.Should().BeTrue();
        _users.Verify(u => u.AddPurchasedCredits(It.IsAny<string>(), It.IsAny<float>()), Times.Never);
    }

    [Fact]
    public async Task SubscriptionUpdated_UpgradeWithMonthlyToAnnualSwitch_GrantsDeltaAndBonus()
    {
        // Pro monthly → Premium annual: the upgrade delta plus the switch bonus (the year
        // commitment is new), as two independently idempotent ledger rows.
        SetupUser(UserRole.Pro);
        _users.Setup(u => u.ApplyUpgradeGrant("user-1", UserRole.Premium, 5000, 4000, "year")).ReturnsAsync(true);
        _users.Setup(u => u.AddPurchasedCredits("user-1", 5000)).ReturnsAsync(true);
        var entries = new List<BillingLedgerRecord>();
        _ledger.Setup(l => l.TryAppend(It.IsAny<BillingLedgerRecord>()))
            .Callback<BillingLedgerRecord>(entries.Add)
            .ReturnsAsync(true);

        var result = await _processor.Process(ParseEvent(
            SubscriptionJson("customer.subscription.updated", "price_premium_annual", previousPriceId: "price_pro")));

        result.Should().BeTrue();
        _users.Verify(u => u.ApplyUpgradeGrant("user-1", UserRole.Premium, 5000, 4000, "year"), Times.Once);
        _users.Verify(u => u.AddPurchasedCredits("user-1", 5000), Times.Once);
        entries.Should().HaveCount(2);
        entries[0].Type.Should().Be(BillingLedgerEntryType.UpgradeGrant);
        entries[1].Type.Should().Be(BillingLedgerEntryType.AnnualBonus);
        entries[1].EventKey.Should().EndWith("#evt_sub_1#bonus");
    }

    [Fact]
    public async Task SubscriptionUpdated_AnnualToAnnualUpgrade_GrantsDeltaWithoutBonus()
    {
        // The year commitment was already rewarded; only the tier delta applies.
        SetupUser(UserRole.Pro);
        _users.Setup(u => u.ApplyUpgradeGrant("user-1", UserRole.Premium, 5000, 4000, "year")).ReturnsAsync(true);
        var entries = new List<BillingLedgerRecord>();
        _ledger.Setup(l => l.TryAppend(It.IsAny<BillingLedgerRecord>()))
            .Callback<BillingLedgerRecord>(entries.Add)
            .ReturnsAsync(true);

        var result = await _processor.Process(ParseEvent(
            SubscriptionJson("customer.subscription.updated", "price_premium_annual", previousPriceId: "price_pro_annual", previousInterval: "year")));

        result.Should().BeTrue();
        _users.Verify(u => u.ApplyUpgradeGrant("user-1", UserRole.Premium, 5000, 4000, "year"), Times.Once);
        _users.Verify(u => u.AddPurchasedCredits(It.IsAny<string>(), It.IsAny<float>()), Times.Never);
        entries.Should().ContainSingle().Which.Type.Should().Be(BillingLedgerEntryType.UpgradeGrant);
    }

    [Fact]
    public async Task SubscriptionUpdated_AnnualSignup_WithoutPreviousPrice_GrantsNoBonus()
    {
        // A fresh annual signup can emit subscription.updated (e.g. incomplete→active) with
        // no price change in previous_attributes; the signup bonus rides invoice.paid, so
        // granting here would double it.
        SetupUser(UserRole.Pro);

        var result = await _processor.Process(ParseEvent(
            SubscriptionJson("customer.subscription.updated", "price_pro_annual")));

        result.Should().BeTrue();
        _ledger.Verify(l => l.TryAppend(It.IsAny<BillingLedgerRecord>()), Times.Never);
        _users.Verify(u => u.AddPurchasedCredits(It.IsAny<string>(), It.IsAny<float>()), Times.Never);
        _users.Verify(u => u.SetBillingInterval(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SubscriptionUpdated_AnnualToMonthlySwitch_DoesNothingUntilRenewal()
    {
        // Interval-lengthening is immediate, shortening is scheduled: the flip back to
        // "month" lands via invoice.paid at the period-end renewal.
        SetupUser(UserRole.Pro);

        var result = await _processor.Process(ParseEvent(
            SubscriptionJson("customer.subscription.updated", "price_pro", previousPriceId: "price_pro_annual", previousInterval: "year")));

        result.Should().BeTrue();
        _ledger.Verify(l => l.TryAppend(It.IsAny<BillingLedgerRecord>()), Times.Never);
        _users.Verify(u => u.SetBillingInterval(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SubscriptionUpdated_ScheduledDowngrade_DoesNothingUntilRenewal()
    {
        SetupUser(UserRole.Premium);

        var result = await _processor.Process(ParseEvent(SubscriptionJson("customer.subscription.updated", "price_pro")));

        result.Should().BeTrue();
        _ledger.Verify(l => l.TryAppend(It.IsAny<BillingLedgerRecord>()), Times.Never);
        _users.Verify(u => u.ApplyUpgradeGrant(It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SubscriptionUpdated_SameTier_DoesNothing()
    {
        SetupUser(UserRole.Pro);

        var result = await _processor.Process(ParseEvent(SubscriptionJson("customer.subscription.updated", "price_pro")));

        result.Should().BeTrue();
        _ledger.Verify(l => l.TryAppend(It.IsAny<BillingLedgerRecord>()), Times.Never);
    }

    [Fact]
    public async Task SubscriptionDeleted_DropsToFreeWithClampedCredits()
    {
        _users.Setup(u => u.CancelSubscription("user-1", 100)).ReturnsAsync(true);

        var result = await _processor.Process(ParseEvent(SubscriptionJson("customer.subscription.deleted", "price_pro")));

        result.Should().BeTrue();
        _users.Verify(u => u.CancelSubscription("user-1", 100), Times.Once);
    }

    [Fact]
    public async Task InvoicePaymentFailed_SetsPastDueOnly()
    {
        _users.Setup(u => u.SetSubscriptionStatus("user-1", "past_due")).ReturnsAsync(true);

        var result = await _processor.Process(ParseEvent(InvoiceJson("invoice.payment_failed", "subscription_cycle")));

        result.Should().BeTrue();
        _users.Verify(u => u.SetSubscriptionStatus("user-1", "past_due"), Times.Once);
        _users.Verify(u => u.ApplySubscriptionGrant(It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<float>(), It.IsAny<string>()), Times.Never);
        _users.Verify(u => u.CancelSubscription(It.IsAny<string>(), It.IsAny<float>()), Times.Never);
    }

    [Fact]
    public async Task ChargeRefunded_WritesNegativeLedgerRowWithoutClawback()
    {
        BillingLedgerRecord entry = null;
        _ledger.Setup(l => l.TryAppend(It.IsAny<BillingLedgerRecord>()))
            .Callback<BillingLedgerRecord>(e => entry = e)
            .ReturnsAsync(true);

        var json = """
        {
          "id": "evt_refund_1",
          "object": "event",
          "api_version": "2026-01-01",
          "created": 1756036800,
          "livemode": false,
          "type": "charge.refunded",
          "data": {
            "object": {
              "id": "ch_1",
              "object": "charge",
              "customer": "cus_1",
              "amount": 1000,
              "amount_refunded": 1000,
              "refunded": true,
              "payment_intent": "pi_1",
              "metadata": { "userId": "user-1" }
            }
          }
        }
        """;

        var result = await _processor.Process(ParseEvent(json));

        result.Should().BeTrue();
        entry.Type.Should().Be(BillingLedgerEntryType.Refund);
        entry.AmountCents.Should().Be(-1000);
        entry.Credits.Should().Be(0);
        _users.Verify(u => u.AddPurchasedCredits(It.IsAny<string>(), It.IsAny<float>()), Times.Never);
        _users.Verify(u => u.TryDebitCredits(It.IsAny<string>(), It.IsAny<float>()), Times.Never);
    }

    [Fact]
    public async Task UnresolvableUser_IsSwallowedWithoutRetry()
    {
        _gateway.Setup(g => g.GetCustomer("cus_1")).ReturnsAsync(new Customer { Id = "cus_1" });

        var result = await _processor.Process(ParseEvent(InvoiceJson("invoice.paid", "subscription_cycle", withMetadata: false)));

        result.Should().BeTrue();
        _ledger.Verify(l => l.TryAppend(It.IsAny<BillingLedgerRecord>()), Times.Never);
    }

    [Fact]
    public async Task UnhandledEventType_IsIgnored()
    {
        var json = """
        {
          "id": "evt_other_1",
          "object": "event",
          "api_version": "2026-01-01",
          "created": 1756036800,
          "livemode": false,
          "type": "customer.created",
          "data": {
            "object": { "id": "cus_1", "object": "customer" }
          }
        }
        """;

        var result = await _processor.Process(ParseEvent(json));

        result.Should().BeTrue();
        _ledger.Verify(l => l.TryAppend(It.IsAny<BillingLedgerRecord>()), Times.Never);
        _users.VerifyNoOtherCalls();
    }
}
