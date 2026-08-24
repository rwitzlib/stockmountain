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

    private static string SubscriptionJson(string type, string priceId)
    {
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
            }
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
        _users.Verify(u => u.ApplySubscriptionGrant(It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<float>()), Times.Never);
    }

    [Fact]
    public async Task InvoicePaid_RenewalGrantsMonthlyCredits()
    {
        _users.Setup(u => u.ApplySubscriptionGrant("user-1", UserRole.Pro, 1000)).ReturnsAsync(true);
        BillingLedgerRecord entry = null;
        _ledger.Setup(l => l.TryAppend(It.IsAny<BillingLedgerRecord>()))
            .Callback<BillingLedgerRecord>(e => entry = e)
            .ReturnsAsync(true);

        var result = await _processor.Process(ParseEvent(InvoiceJson("invoice.paid", "subscription_cycle")));

        result.Should().BeTrue();
        _users.Verify(u => u.ApplySubscriptionGrant("user-1", UserRole.Pro, 1000), Times.Once);
        entry.Type.Should().Be(BillingLedgerEntryType.SubscriptionPayment);
        entry.AmountCents.Should().Be(2900);
        entry.Credits.Should().Be(1000);
        entry.StripeInvoiceId.Should().Be("in_1");
        entry.Tier.Should().Be("Pro");
    }

    [Fact]
    public async Task InvoicePaid_UpgradeProration_IsMoneyOnly()
    {
        BillingLedgerRecord entry = null;
        _ledger.Setup(l => l.TryAppend(It.IsAny<BillingLedgerRecord>()))
            .Callback<BillingLedgerRecord>(e => entry = e)
            .ReturnsAsync(true);

        var result = await _processor.Process(ParseEvent(InvoiceJson("invoice.paid", "subscription_update", "price_premium")));

        result.Should().BeTrue();
        _users.Verify(u => u.ApplySubscriptionGrant(It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<float>()), Times.Never);
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
        _users.Setup(u => u.ApplySubscriptionGrant("user-1", UserRole.Pro, 1000)).ReturnsAsync(true);

        var result = await _processor.Process(ParseEvent(InvoiceJson("invoice.paid", "subscription_cycle", withMetadata: false)));

        result.Should().BeTrue();
        _users.Verify(u => u.ApplySubscriptionGrant("user-1", UserRole.Pro, 1000), Times.Once);
    }

    [Fact]
    public async Task InvoicePaid_GrantFails_SignalsRetry()
    {
        _users.Setup(u => u.ApplySubscriptionGrant("user-1", UserRole.Pro, 1000)).ReturnsAsync(false);

        var result = await _processor.Process(ParseEvent(InvoiceJson("invoice.paid", "subscription_cycle")));

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SubscriptionUpdated_Upgrade_GrantsDeltaImmediately()
    {
        SetupUser(UserRole.Pro);
        _users.Setup(u => u.ApplyUpgradeGrant("user-1", UserRole.Premium, 5000, 4000)).ReturnsAsync(true);
        BillingLedgerRecord entry = null;
        _ledger.Setup(l => l.TryAppend(It.IsAny<BillingLedgerRecord>()))
            .Callback<BillingLedgerRecord>(e => entry = e)
            .ReturnsAsync(true);

        var result = await _processor.Process(ParseEvent(SubscriptionJson("customer.subscription.updated", "price_premium")));

        result.Should().BeTrue();
        _users.Verify(u => u.ApplyUpgradeGrant("user-1", UserRole.Premium, 5000, 4000), Times.Once);
        entry.Type.Should().Be(BillingLedgerEntryType.UpgradeGrant);
        entry.Credits.Should().Be(4000);
        entry.AmountCents.Should().Be(0);
        entry.Tier.Should().Be("Premium");
    }

    [Fact]
    public async Task SubscriptionUpdated_ScheduledDowngrade_DoesNothingUntilRenewal()
    {
        SetupUser(UserRole.Premium);

        var result = await _processor.Process(ParseEvent(SubscriptionJson("customer.subscription.updated", "price_pro")));

        result.Should().BeTrue();
        _ledger.Verify(l => l.TryAppend(It.IsAny<BillingLedgerRecord>()), Times.Never);
        _users.Verify(u => u.ApplyUpgradeGrant(It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<float>(), It.IsAny<float>()), Times.Never);
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
        _users.Verify(u => u.ApplySubscriptionGrant(It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<float>()), Times.Never);
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
