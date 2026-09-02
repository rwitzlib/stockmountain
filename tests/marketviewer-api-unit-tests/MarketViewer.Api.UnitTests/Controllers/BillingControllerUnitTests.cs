using FluentAssertions;
using MarketViewer.Api.Config;
using MarketViewer.Api.Controllers.Billing;
using MarketViewer.Api.Services.Billing;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Records;
using MarketViewer.Contracts.Requests.Billing;
using MarketViewer.Contracts.Responses.Billing;
using MarketViewer.Core.Auth;
using MarketViewer.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Stripe;
using System.Net;
using Xunit;

namespace MarketViewer.Api.UnitTests.Controllers;

public class BillingControllerUnitTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IStripeGateway> _gateway = new();
    private readonly BillingController _classUnderTest;

    public BillingControllerUnitTests()
    {
        var catalog = new BillingCatalog(
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

        _classUnderTest = new BillingController(
            _users.Object,
            _gateway.Object,
            catalog,
            Options.Create(new StripeConfig { SecretKey = "sk_test_unit", ReturnUrlBase = "https://app.test/" }),
            new AuthContext { UserId = "user-1", IsAuthenticated = true },
            NullLogger<BillingController>.Instance);
    }

    private UserRecord SetupUser(UserRole role = UserRole.Free, string stripeCustomerId = null, string subscriptionStatus = null)
    {
        var user = new UserRecord
        {
            Id = "user-1",
            Role = role,
            Credits = 73,
            MaxCredits = 100,
            PurchasedCredits = 250,
            StripeCustomerId = stripeCustomerId,
            SubscriptionStatus = subscriptionStatus
        };
        _users.Setup(u => u.Get("user-1")).ReturnsAsync(user);
        return user;
    }

    [Fact]
    public async Task CheckoutSession_FirstPurchase_CreatesAndLinksStripeCustomer()
    {
        SetupUser();
        _gateway.Setup(g => g.CreateCustomer("user-1")).ReturnsAsync("cus_new");
        _users.Setup(u => u.SetStripeCustomerId("user-1", "cus_new")).ReturnsAsync(true);
        CheckoutSessionSpec spec = null;
        _gateway.Setup(g => g.CreateCheckoutSession(It.IsAny<CheckoutSessionSpec>()))
            .Callback<CheckoutSessionSpec>(s => spec = s)
            .ReturnsAsync("cs_secret_1");

        var result = await _classUnderTest.CreateCheckoutSession(new CheckoutSessionRequest
        {
            Kind = CheckoutKind.Subscription,
            Id = "Pro"
        });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<CheckoutSessionResponse>()
            .Which.ClientSecret.Should().Be("cs_secret_1");
        _users.Verify(u => u.SetStripeCustomerId("user-1", "cus_new"), Times.Once);
        spec.CustomerId.Should().Be("cus_new");
        spec.PriceId.Should().Be("price_pro");
        spec.IsSubscription.Should().BeTrue();
        spec.PackId.Should().BeNull();
    }

    [Fact]
    public async Task CheckoutSession_LostCustomerLinkRace_ReusesStoredCustomer()
    {
        _gateway.Setup(g => g.CreateCustomer("user-1")).ReturnsAsync("cus_loser");
        // A concurrent request linked its customer first; the conditional write refuses ours.
        _users.Setup(u => u.SetStripeCustomerId("user-1", "cus_loser")).ReturnsAsync(false);
        var reads = 0;
        _users.Setup(u => u.Get("user-1")).ReturnsAsync(() => new UserRecord
        {
            Id = "user-1",
            Role = UserRole.Free,
            StripeCustomerId = ++reads == 1 ? null : "cus_winner"
        });
        CheckoutSessionSpec spec = null;
        _gateway.Setup(g => g.CreateCheckoutSession(It.IsAny<CheckoutSessionSpec>()))
            .Callback<CheckoutSessionSpec>(s => spec = s)
            .ReturnsAsync("cs_secret_4");

        var result = await _classUnderTest.CreateCheckoutSession(new CheckoutSessionRequest
        {
            Kind = CheckoutKind.Subscription,
            Id = "Pro"
        });

        result.Should().BeOfType<OkObjectResult>();
        spec.CustomerId.Should().Be("cus_winner");
    }

    [Fact]
    public async Task CheckoutSession_Pack_UsesPaymentModeAndExistingCustomer()
    {
        SetupUser(stripeCustomerId: "cus_1");
        CheckoutSessionSpec spec = null;
        _gateway.Setup(g => g.CreateCheckoutSession(It.IsAny<CheckoutSessionSpec>()))
            .Callback<CheckoutSessionSpec>(s => spec = s)
            .ReturnsAsync("cs_secret_2");

        var result = await _classUnderTest.CreateCheckoutSession(new CheckoutSessionRequest
        {
            Kind = CheckoutKind.Pack,
            Id = "PackSmall"
        });

        result.Should().BeOfType<OkObjectResult>();
        _gateway.Verify(g => g.CreateCustomer(It.IsAny<string>()), Times.Never);
        spec.CustomerId.Should().Be("cus_1");
        spec.PriceId.Should().Be("price_pack_small");
        spec.IsSubscription.Should().BeFalse();
        spec.PackId.Should().Be("PackSmall");
    }

    [Fact]
    public async Task CheckoutSession_PackForActiveSubscriber_IsAllowed()
    {
        SetupUser(role: UserRole.Pro, stripeCustomerId: "cus_1", subscriptionStatus: "active");
        _gateway.Setup(g => g.CreateCheckoutSession(It.IsAny<CheckoutSessionSpec>()))
            .ReturnsAsync("cs_secret_3");

        var result = await _classUnderTest.CreateCheckoutSession(new CheckoutSessionRequest
        {
            Kind = CheckoutKind.Pack,
            Id = "PackLarge"
        });

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CheckoutSession_SubscriptionWhileSubscribed_IsRejected()
    {
        SetupUser(role: UserRole.Pro, stripeCustomerId: "cus_1", subscriptionStatus: "active");

        var result = await _classUnderTest.CreateCheckoutSession(new CheckoutSessionRequest
        {
            Kind = CheckoutKind.Subscription,
            Id = "Premium"
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        _gateway.Verify(g => g.CreateCheckoutSession(It.IsAny<CheckoutSessionSpec>()), Times.Never);
    }

    [Fact]
    public async Task CheckoutSession_LostRaceToSubscribedCustomer_IsRejected()
    {
        // First-purchase race where the winning request's checkout already produced a
        // subscription: the loser adopts the winner's customer and must still be
        // stopped by the live-subscription guard (it runs on the RESOLVED customer).
        _gateway.Setup(g => g.CreateCustomer("user-1")).ReturnsAsync("cus_loser");
        _users.Setup(u => u.SetStripeCustomerId("user-1", "cus_loser")).ReturnsAsync(false);
        var reads = 0;
        _users.Setup(u => u.Get("user-1")).ReturnsAsync(() => new UserRecord
        {
            Id = "user-1",
            Role = UserRole.Free,
            StripeCustomerId = ++reads == 1 ? null : "cus_winner"
        });
        _gateway.Setup(g => g.HasLiveSubscription("cus_winner")).ReturnsAsync(true);

        var result = await _classUnderTest.CreateCheckoutSession(new CheckoutSessionRequest
        {
            Kind = CheckoutKind.Subscription,
            Id = "Pro"
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        _gateway.Verify(g => g.CreateCheckoutSession(It.IsAny<CheckoutSessionSpec>()), Times.Never);
    }

    [Fact]
    public async Task CheckoutSession_SubscriptionDuringWebhookLag_IsRejected()
    {
        // Payment completed but the webhook hasn't set SubscriptionStatus yet: our
        // record still says no subscription, Stripe already has one.
        SetupUser(stripeCustomerId: "cus_1", subscriptionStatus: null);
        _gateway.Setup(g => g.HasLiveSubscription("cus_1")).ReturnsAsync(true);

        var result = await _classUnderTest.CreateCheckoutSession(new CheckoutSessionRequest
        {
            Kind = CheckoutKind.Subscription,
            Id = "Pro"
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        _gateway.Verify(g => g.CreateCheckoutSession(It.IsAny<CheckoutSessionSpec>()), Times.Never);
    }

    [Fact]
    public async Task CheckoutSession_PackDuringWebhookLag_IsAllowed()
    {
        // The live-subscription guard only applies to subscription checkouts.
        SetupUser(stripeCustomerId: "cus_1", subscriptionStatus: null);
        _gateway.Setup(g => g.HasLiveSubscription("cus_1")).ReturnsAsync(true);
        _gateway.Setup(g => g.CreateCheckoutSession(It.IsAny<CheckoutSessionSpec>()))
            .ReturnsAsync("cs_secret_5");

        var result = await _classUnderTest.CreateCheckoutSession(new CheckoutSessionRequest
        {
            Kind = CheckoutKind.Pack,
            Id = "PackSmall"
        });

        result.Should().BeOfType<OkObjectResult>();
    }

    [Theory]
    [InlineData("ProAnnual", "price_pro_annual")]
    [InlineData("PremiumAnnual", "price_premium_annual")]
    public async Task CheckoutSession_AnnualTier_IsAcceptedAndUsesAnnualPrice(string id, string expectedPriceId)
    {
        SetupUser(stripeCustomerId: "cus_1");
        CheckoutSessionSpec spec = null;
        _gateway.Setup(g => g.CreateCheckoutSession(It.IsAny<CheckoutSessionSpec>()))
            .Callback<CheckoutSessionSpec>(s => spec = s)
            .ReturnsAsync("cs_secret_annual");

        var result = await _classUnderTest.CreateCheckoutSession(new CheckoutSessionRequest
        {
            Kind = CheckoutKind.Subscription,
            Id = id
        });

        result.Should().BeOfType<OkObjectResult>();
        spec.PriceId.Should().Be(expectedPriceId);
        spec.IsSubscription.Should().BeTrue();
    }

    [Theory]
    [InlineData(CheckoutKind.Subscription, "Free")]
    [InlineData(CheckoutKind.Subscription, "FreeAnnual")]
    [InlineData(CheckoutKind.Subscription, "GoldAnnual")]
    [InlineData(CheckoutKind.Subscription, "ProAnnualPromo")]
    [InlineData(CheckoutKind.Subscription, "Gold")]
    [InlineData(CheckoutKind.Pack, "PackHuge")]
    public async Task CheckoutSession_UnknownItem_IsRejected(CheckoutKind kind, string id)
    {
        SetupUser();

        var result = await _classUnderTest.CreateCheckoutSession(new CheckoutSessionRequest { Kind = kind, Id = id });

        result.Should().BeOfType<BadRequestObjectResult>();
        _gateway.Verify(g => g.CreateCheckoutSession(It.IsAny<CheckoutSessionSpec>()), Times.Never);
    }

    [Fact]
    public async Task CheckoutSession_MissingPriceConfig_Returns500()
    {
        SetupUser();
        var controllerWithoutPrices = new BillingController(
            _users.Object,
            _gateway.Object,
            new BillingCatalog(
                new Dictionary<UserRole, float> { { UserRole.Pro, 1000 } },
                new Dictionary<string, float>(),
                new Dictionary<string, string>()),
            Options.Create(new StripeConfig { SecretKey = "sk_test_unit", ReturnUrlBase = "https://app.test" }),
            new AuthContext { UserId = "user-1", IsAuthenticated = true },
            NullLogger<BillingController>.Instance);

        var result = await controllerWithoutPrices.CreateCheckoutSession(new CheckoutSessionRequest
        {
            Kind = CheckoutKind.Subscription,
            Id = "Pro"
        });

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task CheckoutSession_MissingSecretKey_Returns500WithoutCallingStripe()
    {
        SetupUser();
        var controllerWithoutKey = new BillingController(
            _users.Object,
            _gateway.Object,
            new BillingCatalog(
                new Dictionary<UserRole, float>(),
                new Dictionary<string, float> { { "PackSmall", 250 } },
                new Dictionary<string, string> { { "PackSmall", "price_pack_small" } }),
            Options.Create(new StripeConfig { ReturnUrlBase = "https://app.test" }),
            new AuthContext { UserId = "user-1", IsAuthenticated = true },
            NullLogger<BillingController>.Instance);

        var result = await controllerWithoutKey.CreateCheckoutSession(new CheckoutSessionRequest
        {
            Kind = CheckoutKind.Pack,
            Id = "PackSmall"
        });

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        objectResult.Value.Should().BeEquivalentTo(new[] { "Billing is not configured" });
        _gateway.Verify(g => g.CreateCustomer(It.IsAny<string>()), Times.Never);
        _gateway.Verify(g => g.CreateCheckoutSession(It.IsAny<CheckoutSessionSpec>()), Times.Never);
    }

    [Fact]
    public async Task CheckoutSession_StripeRejection_Returns502WithStripeMessage()
    {
        SetupUser(stripeCustomerId: "cus_existing");
        _gateway.Setup(g => g.CreateCheckoutSession(It.IsAny<CheckoutSessionSpec>()))
            .ThrowsAsync(new StripeException(
                HttpStatusCode.BadRequest,
                new StripeError { Code = "resource_missing", Message = "No such price: 'price_pack_small'" },
                "No such price: 'price_pack_small'"));

        var result = await _classUnderTest.CreateCheckoutSession(new CheckoutSessionRequest
        {
            Kind = CheckoutKind.Pack,
            Id = "PackSmall"
        });

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
        objectResult.Value.Should().BeEquivalentTo(
            new[] { "Stripe rejected the checkout request: No such price: 'price_pack_small'" });
    }

    [Fact]
    public async Task CheckoutSession_StripeRejectionWithoutErrorBody_UsesExceptionMessage()
    {
        SetupUser();
        _gateway.Setup(g => g.CreateCustomer("user-1"))
            .ThrowsAsync(new StripeException("Invalid API Key provided: sk_test_****unit"));

        var result = await _classUnderTest.CreateCheckoutSession(new CheckoutSessionRequest
        {
            Kind = CheckoutKind.Pack,
            Id = "PackSmall"
        });

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
        objectResult.Value.Should().BeEquivalentTo(
            new[] { "Stripe rejected the checkout request: Invalid API Key provided: sk_test_****unit" });
    }

    [Fact]
    public async Task PortalSession_StripeRejection_Returns502WithStripeMessage()
    {
        SetupUser(stripeCustomerId: "cus_1");
        _gateway.Setup(g => g.CreatePortalSession("cus_1", It.IsAny<string>()))
            .ThrowsAsync(new StripeException(
                HttpStatusCode.BadRequest,
                new StripeError { Code = "resource_missing", Message = "No such customer: 'cus_1'" },
                "No such customer: 'cus_1'"));

        var result = await _classUnderTest.CreatePortalSession();

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
        objectResult.Value.Should().BeEquivalentTo(
            new[] { "Stripe rejected the portal request: No such customer: 'cus_1'" });
    }

    [Fact]
    public async Task PortalSession_WithoutBillingAccount_IsRejected()
    {
        SetupUser(stripeCustomerId: null);

        var result = await _classUnderTest.CreatePortalSession();

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PortalSession_ReturnsPortalUrl()
    {
        SetupUser(stripeCustomerId: "cus_1");
        _gateway.Setup(g => g.CreatePortalSession("cus_1", "https://app.test/billing"))
            .ReturnsAsync("https://billing.stripe.com/portal_1");

        var result = await _classUnderTest.CreatePortalSession();

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<PortalSessionResponse>()
            .Which.Url.Should().Be("https://billing.stripe.com/portal_1");
    }

    [Fact]
    public async Task Summary_MapsUserRecordWithDefaultStatus()
    {
        SetupUser(role: UserRole.Pro);

        var result = await _classUnderTest.GetSummary();

        var summary = result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<BillingSummaryResponse>().Subject;
        summary.Tier.Should().Be(UserRole.Pro);
        summary.Credits.Should().Be(73);
        summary.MaxCredits.Should().Be(100);
        summary.PurchasedCredits.Should().Be(250);
        summary.SubscriptionStatus.Should().Be("none");
        summary.HasBillingAccount.Should().BeFalse();
    }

    [Fact]
    public async Task Summary_LinkedStripeCustomer_ReportsBillingAccount()
    {
        SetupUser(role: UserRole.Pro, stripeCustomerId: "cus_1", subscriptionStatus: "active");

        var result = await _classUnderTest.GetSummary();

        var summary = result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<BillingSummaryResponse>().Subject;
        summary.SubscriptionStatus.Should().Be("active");
        summary.HasBillingAccount.Should().BeTrue();
    }

    [Fact]
    public async Task Summary_UnknownUser_Returns404()
    {
        _users.Setup(u => u.Get("user-1")).ReturnsAsync((UserRecord)null);

        var result = await _classUnderTest.GetSummary();

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
