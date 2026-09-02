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

    private LiveSubscription SetupSubscription(
        string priceId = "price_pro",
        string status = "active",
        bool cancelAtPeriodEnd = false,
        string scheduleId = null,
        string scheduledPriceId = null)
    {
        var subscription = new LiveSubscription
        {
            Id = "sub_1",
            CustomerId = "cus_1",
            ItemId = "si_1",
            PriceId = priceId,
            Status = status,
            CurrentPeriodEnd = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            CancelAtPeriodEnd = cancelAtPeriodEnd,
            Metadata = new Dictionary<string, string> { { "userId", "user-1" } },
            ScheduleId = scheduleId,
            ScheduledPriceId = scheduledPriceId,
            ScheduledStartsAt = scheduledPriceId is null ? null : new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        _gateway.Setup(g => g.GetLiveSubscription("cus_1")).ReturnsAsync(subscription);
        return subscription;
    }

    [Fact]
    public async Task GetSubscription_WithoutBillingAccount_ReportsNoneWithoutCallingStripe()
    {
        SetupUser();

        var result = await _classUnderTest.GetSubscription();

        var response = result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<SubscriptionDetailsResponse>().Subject;
        response.HasSubscription.Should().BeFalse();
        _gateway.Verify(g => g.GetLiveSubscription(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetSubscription_MapsLiveSubscriptionAndScheduledChange()
    {
        SetupUser(UserRole.Premium, stripeCustomerId: "cus_1", subscriptionStatus: "active");
        SetupSubscription(priceId: "price_premium", scheduleId: "sub_sched_1", scheduledPriceId: "price_pro_annual");

        var result = await _classUnderTest.GetSubscription();

        var response = result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<SubscriptionDetailsResponse>().Subject;
        response.HasSubscription.Should().BeTrue();
        response.Tier.Should().Be(UserRole.Premium);
        response.Interval.Should().Be(BillingInterval.Month);
        response.Status.Should().Be("active");
        response.CurrentPeriodEnd.Should().Be(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        response.PendingChange.Should().NotBeNull();
        response.PendingChange.Tier.Should().Be(UserRole.Pro);
        response.PendingChange.Interval.Should().Be(BillingInterval.Year);
        response.PendingChange.EffectiveAt.Should().Be(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetSubscription_NoLiveSubscription_ReportsNone()
    {
        SetupUser(stripeCustomerId: "cus_1");
        _gateway.Setup(g => g.GetLiveSubscription("cus_1")).ReturnsAsync((LiveSubscription)null);

        var result = await _classUnderTest.GetSubscription();

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<SubscriptionDetailsResponse>()
            .Which.HasSubscription.Should().BeFalse();
    }

    [Fact]
    public async Task PlanChangePreview_Upgrade_ReturnsProratedAmountDueNow()
    {
        SetupUser(UserRole.Pro, stripeCustomerId: "cus_1", subscriptionStatus: "active");
        var subscription = SetupSubscription(priceId: "price_pro");
        _gateway.Setup(g => g.PreviewImmediateChange(subscription, "price_premium"))
            .ReturnsAsync(new ProrationPreview { AmountDueCents = 4550, Currency = "usd" });

        var result = await _classUnderTest.PreviewPlanChange(new PlanChangeRequest { Id = "Premium" });

        var preview = result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<PlanChangePreviewResponse>().Subject;
        preview.Timing.Should().Be(PlanChangeTiming.Immediate);
        preview.NewTier.Should().Be(UserRole.Premium);
        preview.NewInterval.Should().Be(BillingInterval.Month);
        preview.AmountDueCents.Should().Be(4550);
        preview.Currency.Should().Be("usd");
    }

    [Theory]
    [InlineData("price_premium", "Pro", UserRole.Pro, BillingInterval.Month)]
    [InlineData("price_pro_annual", "Pro", UserRole.Pro, BillingInterval.Month)]
    [InlineData("price_premium", "ProAnnual", UserRole.Pro, BillingInterval.Year)]
    public async Task PlanChangePreview_Downgrade_IsFreeAndTakesEffectAtPeriodEnd(string currentPriceId, string targetId, UserRole expectedTier, string expectedInterval)
    {
        SetupUser(UserRole.Premium, stripeCustomerId: "cus_1", subscriptionStatus: "active");
        SetupSubscription(priceId: currentPriceId);

        var result = await _classUnderTest.PreviewPlanChange(new PlanChangeRequest { Id = targetId });

        var preview = result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<PlanChangePreviewResponse>().Subject;
        preview.Timing.Should().Be(PlanChangeTiming.PeriodEnd);
        preview.NewTier.Should().Be(expectedTier);
        preview.NewInterval.Should().Be(expectedInterval);
        preview.AmountDueCents.Should().Be(0);
        preview.EffectiveAt.Should().Be(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        _gateway.Verify(g => g.PreviewImmediateChange(It.IsAny<LiveSubscription>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PlanChange_Upgrade_AppliesImmediatelyAndDropsAnyScheduledDowngrade()
    {
        SetupUser(UserRole.Pro, stripeCustomerId: "cus_1", subscriptionStatus: "active");
        var subscription = SetupSubscription(priceId: "price_pro", scheduleId: "sub_sched_1", scheduledPriceId: "price_pro");
        _gateway.Setup(g => g.ChangePlanNow(subscription, "price_premium"))
            .ReturnsAsync(new ImmediateChangeResult { Applied = true });

        var result = await _classUnderTest.ChangePlan(new PlanChangeRequest { Id = "Premium" });

        var response = result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<PlanChangeResponse>().Subject;
        response.Status.Should().Be(PlanChangeStatus.Applied);
        response.PaymentUrl.Should().BeNull();
        _gateway.Verify(g => g.ReleaseScheduledChange(subscription), Times.Once);
        _gateway.Verify(g => g.ChangePlanNow(subscription, "price_premium"), Times.Once);
        _gateway.Verify(g => g.SchedulePlanChangeAtPeriodEnd(It.IsAny<LiveSubscription>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PlanChange_SameTierMonthlyToAnnual_AppliesImmediately()
    {
        SetupUser(UserRole.Pro, stripeCustomerId: "cus_1", subscriptionStatus: "active");
        var subscription = SetupSubscription(priceId: "price_pro");
        _gateway.Setup(g => g.ChangePlanNow(subscription, "price_pro_annual"))
            .ReturnsAsync(new ImmediateChangeResult { Applied = true });

        var result = await _classUnderTest.ChangePlan(new PlanChangeRequest { Id = "ProAnnual" });

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<PlanChangeResponse>()
            .Which.Status.Should().Be(PlanChangeStatus.Applied);
        _gateway.Verify(g => g.ChangePlanNow(subscription, "price_pro_annual"), Times.Once);
    }

    [Fact]
    public async Task PlanChange_UpgradeNeedingPaymentAction_ReturnsInvoiceUrlWithoutApplying()
    {
        SetupUser(UserRole.Pro, stripeCustomerId: "cus_1", subscriptionStatus: "active");
        var subscription = SetupSubscription(priceId: "price_pro");
        _gateway.Setup(g => g.ChangePlanNow(subscription, "price_premium"))
            .ReturnsAsync(new ImmediateChangeResult { Applied = false, PaymentUrl = "https://invoice.stripe.com/i/in_1" });

        var result = await _classUnderTest.ChangePlan(new PlanChangeRequest { Id = "Premium" });

        var response = result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<PlanChangeResponse>().Subject;
        response.Status.Should().Be(PlanChangeStatus.RequiresAction);
        response.PaymentUrl.Should().Be("https://invoice.stripe.com/i/in_1");
    }

    [Theory]
    [InlineData("price_premium", "Pro", "price_pro", BillingInterval.Month)]
    [InlineData("price_pro_annual", "Pro", "price_pro", BillingInterval.Month)]
    [InlineData("price_premium_annual", "ProAnnual", "price_pro_annual", BillingInterval.Year)]
    public async Task PlanChange_Downgrade_IsScheduledForPeriodEnd(string currentPriceId, string targetId, string expectedPriceId, string expectedInterval)
    {
        SetupUser(UserRole.Premium, stripeCustomerId: "cus_1", subscriptionStatus: "active");
        var subscription = SetupSubscription(priceId: currentPriceId);
        var effectiveAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        _gateway.Setup(g => g.SchedulePlanChangeAtPeriodEnd(subscription, expectedPriceId, expectedInterval)).ReturnsAsync(effectiveAt);

        var result = await _classUnderTest.ChangePlan(new PlanChangeRequest { Id = targetId });

        var response = result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<PlanChangeResponse>().Subject;
        response.Status.Should().Be(PlanChangeStatus.Scheduled);
        response.EffectiveAt.Should().Be(effectiveAt);
        _gateway.Verify(g => g.ChangePlanNow(It.IsAny<LiveSubscription>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("Free")]
    [InlineData("PackSmall")]
    [InlineData("Enterprise")]
    [InlineData("")]
    public async Task PlanChange_UnknownPlan_IsRejectedBeforeAnyLookup(string id)
    {
        var result = await _classUnderTest.ChangePlan(new PlanChangeRequest { Id = id });

        result.Should().BeOfType<BadRequestObjectResult>();
        _users.Verify(u => u.Get(It.IsAny<string>()), Times.Never);
        _gateway.Verify(g => g.GetLiveSubscription(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PlanChange_SamePlan_IsRejected()
    {
        SetupUser(UserRole.Pro, stripeCustomerId: "cus_1", subscriptionStatus: "active");
        SetupSubscription(priceId: "price_pro");

        var result = await _classUnderTest.ChangePlan(new PlanChangeRequest { Id = "Pro" });

        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().BeEquivalentTo(new[] { "You're already on this plan." });
    }

    [Fact]
    public async Task PlanChange_WithoutLiveSubscription_IsRejected()
    {
        SetupUser(stripeCustomerId: "cus_1");
        _gateway.Setup(g => g.GetLiveSubscription("cus_1")).ReturnsAsync((LiveSubscription)null);

        var result = await _classUnderTest.ChangePlan(new PlanChangeRequest { Id = "Premium" });

        result.Should().BeOfType<BadRequestObjectResult>();
        _gateway.Verify(g => g.ChangePlanNow(It.IsAny<LiveSubscription>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PlanChange_PastDueSubscription_IsRejected()
    {
        SetupUser(UserRole.Pro, stripeCustomerId: "cus_1", subscriptionStatus: "past_due");
        SetupSubscription(priceId: "price_pro", status: "past_due");

        var result = await _classUnderTest.ChangePlan(new PlanChangeRequest { Id = "Premium" });

        result.Should().BeOfType<BadRequestObjectResult>();
        _gateway.Verify(g => g.ChangePlanNow(It.IsAny<LiveSubscription>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PlanChange_CancellingSubscription_IsRejected()
    {
        SetupUser(UserRole.Pro, stripeCustomerId: "cus_1", subscriptionStatus: "active");
        SetupSubscription(priceId: "price_pro", cancelAtPeriodEnd: true);

        var result = await _classUnderTest.ChangePlan(new PlanChangeRequest { Id = "Premium" });

        result.Should().BeOfType<BadRequestObjectResult>();
        _gateway.Verify(g => g.ChangePlanNow(It.IsAny<LiveSubscription>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PlanChange_StripeRejection_Returns502WithStripeMessage()
    {
        SetupUser(UserRole.Pro, stripeCustomerId: "cus_1", subscriptionStatus: "active");
        var subscription = SetupSubscription(priceId: "price_pro");
        _gateway.Setup(g => g.ChangePlanNow(subscription, "price_premium"))
            .ThrowsAsync(new StripeException(
                HttpStatusCode.BadRequest,
                new StripeError { Code = "resource_missing", Message = "No such price: 'price_premium'" },
                "No such price: 'price_premium'"));

        var result = await _classUnderTest.ChangePlan(new PlanChangeRequest { Id = "Premium" });

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
        objectResult.Value.Should().BeEquivalentTo(new[] { "Stripe rejected the plan change: No such price: 'price_premium'" });
    }

    [Fact]
    public async Task CancelScheduledPlanChange_ReleasesTheSchedule()
    {
        SetupUser(UserRole.Premium, stripeCustomerId: "cus_1", subscriptionStatus: "active");
        var subscription = SetupSubscription(priceId: "price_premium", scheduleId: "sub_sched_1", scheduledPriceId: "price_pro");

        var result = await _classUnderTest.CancelScheduledPlanChange();

        result.Should().BeOfType<NoContentResult>();
        _gateway.Verify(g => g.ReleaseScheduledChange(subscription), Times.Once);
    }

    [Fact]
    public async Task CancelScheduledPlanChange_NothingScheduled_IsRejected()
    {
        SetupUser(UserRole.Premium, stripeCustomerId: "cus_1", subscriptionStatus: "active");
        SetupSubscription(priceId: "price_premium");

        var result = await _classUnderTest.CancelScheduledPlanChange();

        result.Should().BeOfType<BadRequestObjectResult>();
        _gateway.Verify(g => g.ReleaseScheduledChange(It.IsAny<LiveSubscription>()), Times.Never);
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
