using MarketViewer.Api.Authorization;
using MarketViewer.Api.Config;
using MarketViewer.Api.Services.Billing;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Records;
using MarketViewer.Contracts.Requests.Billing;
using MarketViewer.Contracts.Responses.Billing;
using MarketViewer.Core.Auth;
using MarketViewer.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;

namespace MarketViewer.Api.Controllers.Billing;

[ApiController]
[Route("billing")]
public class BillingController(
    IUserRepository userRepository,
    IStripeGateway stripeGateway,
    BillingCatalog catalog,
    IOptions<StripeConfig> stripeOptions,
    AuthContext authContext,
    ILogger<BillingController> logger) : ControllerBase
{
    [HttpPost]
    [Route("checkout-session")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize]
    [RequiresTier(UserRole.Free)]
    public async Task<IActionResult> CreateCheckoutSession([FromBody] CheckoutSessionRequest request)
    {
        var user = await userRepository.Get(authContext.UserId);
        if (user is null)
        {
            return NotFound(new[] { "User not found" });
        }

        var isSubscription = request.Kind == CheckoutKind.Subscription;

        if (isSubscription)
        {
            // Accepts "Pro"/"Premium" and their "{Tier}Annual" yearly variants; "Free",
            // pack ids, and garbage are rejected here before any Stripe call.
            if (!BillingCatalog.TryResolveTierFromKey(request.Id, out var tier, out _) || tier == UserRole.Free)
            {
                return BadRequest(new[] { $"Unknown subscription tier '{request.Id}'" });
            }

            // Checkout creates a new subscription; plan changes for existing subscribers
            // happen in the Customer Portal (proration on upgrade, period-end downgrade).
            if (user.SubscriptionStatus is "active" or "past_due")
            {
                return BadRequest(new[] { "You already have a subscription. Use the billing portal to change plans." });
            }
        }
        else if (!catalog.TryGetPackCredits(request.Id, out _))
        {
            return BadRequest(new[] { $"Unknown credit pack '{request.Id}'" });
        }

        if (!catalog.TryGetPriceId(request.Id, out var priceId))
        {
            logger.LogError("No Stripe price configured for '{Id}'", request.Id);
            return StatusCode(StatusCodes.Status500InternalServerError, new[] { "Billing is not configured for this item" });
        }

        if (string.IsNullOrEmpty(stripeOptions.Value.SecretKey))
        {
            logger.LogError("Stripe secret key is not configured; cannot create a checkout session");
            return StatusCode(StatusCodes.Status500InternalServerError, new[] { "Billing is not configured" });
        }

        try
        {
            var customerId = user.StripeCustomerId;
            if (string.IsNullOrEmpty(customerId))
            {
                customerId = await stripeGateway.CreateCustomer(user.Id);
                if (!await userRepository.SetStripeCustomerId(user.Id, customerId))
                {
                    // Lost a concurrent first-checkout race: a parallel request linked its
                    // customer first. Use the stored one so this session lands on the customer
                    // the portal will open; the extra Stripe customer is a harmless orphan.
                    var refreshed = await userRepository.Get(user.Id);
                    if (string.IsNullOrEmpty(refreshed?.StripeCustomerId))
                    {
                        logger.LogError("Failed to link Stripe customer {CustomerId} to user {UserId}", customerId, user.Id);
                        return StatusCode(StatusCodes.Status500InternalServerError, new[] { "Could not set up billing; try again" });
                    }

                    customerId = refreshed.StripeCustomerId;
                }
            }

            // Webhook-lag guard, checked on the RESOLVED customer (covering the race path
            // above where we adopt another request's customer): after a checkout completes,
            // SubscriptionStatus stays non-active until the webhook lands, so the check at
            // the top can't stop a second subscription purchase. Stripe itself is the
            // authoritative record — ask it directly rather than keeping local pending-claim
            // state. Residual risk accepted: two truly concurrent sessions created before
            // EITHER payment exists can't be caught here (nothing exists on Stripe yet) —
            // that needs the same user paying twice in parallel tabs within seconds.
            if (isSubscription && await stripeGateway.HasLiveSubscription(customerId))
            {
                return BadRequest(new[] { "A subscription already exists or is being processed. Use the billing portal to change plans." });
            }

            var clientSecret = await stripeGateway.CreateCheckoutSession(new CheckoutSessionSpec
            {
                UserId = user.Id,
                CustomerId = customerId,
                PriceId = priceId,
                IsSubscription = isSubscription,
                PackId = isSubscription ? null : request.Id
            });

            return Ok(new CheckoutSessionResponse { ClientSecret = clientSecret });
        }
        catch (StripeException ex)
        {
            logger.LogError(ex,
                "Stripe rejected checkout for user {UserId}, item {Id} ({Kind}): {StripeCode} {StripeMessage}",
                user.Id, request.Id, request.Kind, ex.StripeError?.Code, ex.StripeError?.Message);
            return StatusCode(StatusCodes.Status502BadGateway, new[] { $"Stripe rejected the checkout request: {DescribeStripeError(ex)}" });
        }
    }

    [HttpPost]
    [Route("portal-session")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize]
    [RequiresTier(UserRole.Free)]
    public async Task<IActionResult> CreatePortalSession()
    {
        var user = await userRepository.Get(authContext.UserId);
        if (user is null)
        {
            return NotFound(new[] { "User not found" });
        }

        if (string.IsNullOrEmpty(user.StripeCustomerId))
        {
            return BadRequest(new[] { "No billing account yet. Subscribe or buy credits first." });
        }

        var returnUrlBase = stripeOptions.Value.ReturnUrlBase.TrimEnd('/');

        try
        {
            var url = await stripeGateway.CreatePortalSession(user.StripeCustomerId, $"{returnUrlBase}/billing");
            return Ok(new PortalSessionResponse { Url = url });
        }
        catch (StripeException ex)
        {
            logger.LogError(ex,
                "Stripe rejected portal session for user {UserId}: {StripeCode} {StripeMessage}",
                user.Id, ex.StripeError?.Code, ex.StripeError?.Message);
            return StatusCode(StatusCodes.Status502BadGateway, new[] { $"Stripe rejected the portal request: {DescribeStripeError(ex)}" });
        }
    }

    [HttpGet]
    [Route("subscription")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize]
    [RequiresTier(UserRole.Free)]
    public async Task<IActionResult> GetSubscription()
    {
        var user = await userRepository.Get(authContext.UserId);
        if (user is null)
        {
            return NotFound(new[] { "User not found" });
        }

        if (string.IsNullOrEmpty(user.StripeCustomerId) || string.IsNullOrEmpty(stripeOptions.Value.SecretKey))
        {
            return Ok(new SubscriptionDetailsResponse { HasSubscription = false });
        }

        try
        {
            var subscription = await stripeGateway.GetLiveSubscription(user.StripeCustomerId);
            if (subscription is null)
            {
                return Ok(new SubscriptionDetailsResponse { HasSubscription = false });
            }

            if (!catalog.TryResolveTierFromPrice(subscription.PriceId, out var tier, out var interval))
            {
                logger.LogWarning("Subscription {SubscriptionId} for user {UserId} is on unconfigured price {PriceId}",
                    subscription.Id, user.Id, subscription.PriceId);
                return Ok(new SubscriptionDetailsResponse { HasSubscription = false });
            }

            var response = new SubscriptionDetailsResponse
            {
                HasSubscription = true,
                Tier = tier,
                Interval = interval,
                Status = subscription.Status,
                CurrentPeriodEnd = subscription.CurrentPeriodEnd,
                CancelAtPeriodEnd = subscription.CancelAtPeriodEnd
            };

            if (subscription.ScheduledStartsAt is not null
                && catalog.TryResolveTierFromPrice(subscription.ScheduledPriceId, out var pendingTier, out var pendingInterval))
            {
                response.PendingChange = new PendingPlanChange
                {
                    Tier = pendingTier,
                    Interval = pendingInterval,
                    EffectiveAt = subscription.ScheduledStartsAt.Value
                };
            }

            return Ok(response);
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe rejected subscription lookup for user {UserId}: {StripeCode} {StripeMessage}",
                user.Id, ex.StripeError?.Code, ex.StripeError?.Message);
            return StatusCode(StatusCodes.Status502BadGateway, new[] { $"Stripe rejected the subscription lookup: {DescribeStripeError(ex)}" });
        }
    }

    [HttpPost]
    [Route("plan-change/preview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize]
    [RequiresTier(UserRole.Free)]
    public async Task<IActionResult> PreviewPlanChange([FromBody] PlanChangeRequest request)
    {
        var (change, error) = await ResolvePlanChange(request);
        if (error is not null)
        {
            return error;
        }

        try
        {
            if (change.Timing == PlanChangeTiming.PeriodEnd)
            {
                return Ok(new PlanChangePreviewResponse
                {
                    Timing = PlanChangeTiming.PeriodEnd,
                    NewTier = change.TargetTier,
                    NewInterval = change.TargetInterval,
                    AmountDueCents = 0,
                    EffectiveAt = change.Subscription.CurrentPeriodEnd
                });
            }

            var preview = await stripeGateway.PreviewImmediateChange(change.Subscription, change.TargetPriceId);
            return Ok(new PlanChangePreviewResponse
            {
                Timing = PlanChangeTiming.Immediate,
                NewTier = change.TargetTier,
                NewInterval = change.TargetInterval,
                AmountDueCents = preview.AmountDueCents,
                Currency = preview.Currency,
                EffectiveAt = DateTime.UtcNow
            });
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe rejected plan-change preview for user {UserId} to {Id}: {StripeCode} {StripeMessage}",
                change.User.Id, request.Id, ex.StripeError?.Code, ex.StripeError?.Message);
            return StatusCode(StatusCodes.Status502BadGateway, new[] { $"Stripe rejected the plan-change preview: {DescribeStripeError(ex)}" });
        }
    }

    [HttpPost]
    [Route("plan-change")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize]
    [RequiresTier(UserRole.Free)]
    public async Task<IActionResult> ChangePlan([FromBody] PlanChangeRequest request)
    {
        var (change, error) = await ResolvePlanChange(request);
        if (error is not null)
        {
            return error;
        }

        try
        {
            if (change.Timing == PlanChangeTiming.PeriodEnd)
            {
                var effectiveAt = await stripeGateway.SchedulePlanChangeAtPeriodEnd(change.Subscription, change.TargetPriceId, change.TargetInterval);
                logger.LogInformation("Scheduled plan change for user {UserId}: {Current} -> {Target} at {EffectiveAt}",
                    change.User.Id, change.Subscription.PriceId, request.Id, effectiveAt);
                return Ok(new PlanChangeResponse { Status = PlanChangeStatus.Scheduled, EffectiveAt = effectiveAt });
            }

            // An upgrade supersedes any downgrade still waiting for period end.
            await stripeGateway.ReleaseScheduledChange(change.Subscription);

            var result = await stripeGateway.ChangePlanNow(change.Subscription, change.TargetPriceId);
            if (!result.Applied)
            {
                logger.LogInformation("Plan change for user {UserId} to {Id} is pending customer payment action", change.User.Id, request.Id);
                return Ok(new PlanChangeResponse { Status = PlanChangeStatus.RequiresAction, PaymentUrl = result.PaymentUrl });
            }

            logger.LogInformation("Applied plan change for user {UserId}: {Current} -> {Target}",
                change.User.Id, change.Subscription.PriceId, request.Id);
            return Ok(new PlanChangeResponse { Status = PlanChangeStatus.Applied, EffectiveAt = DateTime.UtcNow });
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe rejected plan change for user {UserId} to {Id}: {StripeCode} {StripeMessage}",
                change.User.Id, request.Id, ex.StripeError?.Code, ex.StripeError?.Message);
            return StatusCode(StatusCodes.Status502BadGateway, new[] { $"Stripe rejected the plan change: {DescribeStripeError(ex)}" });
        }
    }

    [HttpDelete]
    [Route("plan-change")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize]
    [RequiresTier(UserRole.Free)]
    public async Task<IActionResult> CancelScheduledPlanChange()
    {
        var user = await userRepository.Get(authContext.UserId);
        if (user is null)
        {
            return NotFound(new[] { "User not found" });
        }

        if (string.IsNullOrEmpty(user.StripeCustomerId))
        {
            return BadRequest(new[] { "No scheduled plan change to cancel." });
        }

        try
        {
            var subscription = await stripeGateway.GetLiveSubscription(user.StripeCustomerId);
            if (subscription?.ScheduledStartsAt is null)
            {
                return BadRequest(new[] { "No scheduled plan change to cancel." });
            }

            await stripeGateway.ReleaseScheduledChange(subscription);
            logger.LogInformation("Cancelled scheduled plan change for user {UserId} (schedule {ScheduleId})", user.Id, subscription.ScheduleId);
            return NoContent();
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe rejected cancelling the scheduled plan change for user {UserId}: {StripeCode} {StripeMessage}",
                user.Id, ex.StripeError?.Code, ex.StripeError?.Message);
            return StatusCode(StatusCodes.Status502BadGateway, new[] { $"Stripe rejected the request: {DescribeStripeError(ex)}" });
        }
    }

    [HttpGet]
    [Route("summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize]
    [RequiresTier(UserRole.Free)]
    public async Task<IActionResult> GetSummary()
    {
        var user = await userRepository.Get(authContext.UserId);
        if (user is null)
        {
            return NotFound(new[] { "User not found" });
        }

        return Ok(new BillingSummaryResponse
        {
            Tier = user.Role,
            Credits = user.Credits,
            MaxCredits = user.MaxCredits,
            PurchasedCredits = user.PurchasedCredits,
            SubscriptionStatus = string.IsNullOrEmpty(user.SubscriptionStatus) ? "none" : user.SubscriptionStatus,
            HasBillingAccount = !string.IsNullOrEmpty(user.StripeCustomerId)
        });
    }

    private sealed class PlanChange
    {
        public UserRecord User { get; init; }
        public LiveSubscription Subscription { get; init; }
        public UserRole TargetTier { get; init; }
        public string TargetInterval { get; init; }
        public string TargetPriceId { get; init; }
        public string Timing { get; init; }
    }

    /// <summary>
    /// Shared validation for preview and apply: the target must be a paid tier price, the
    /// user must hold a live, paid-up, non-cancelling subscription on a configured price,
    /// and the target must differ from it. Returns the classified change or the response
    /// explaining why it can't happen.
    /// </summary>
    private async Task<(PlanChange Change, IActionResult Error)> ResolvePlanChange(PlanChangeRequest request)
    {
        if (!BillingCatalog.TryResolveTierFromKey(request?.Id, out var targetTier, out var targetInterval) || targetTier == UserRole.Free)
        {
            return (null, BadRequest(new[] { $"Unknown subscription plan '{request?.Id}'" }));
        }

        if (!catalog.TryGetPriceId(request.Id, out var targetPriceId))
        {
            logger.LogError("No Stripe price configured for '{Id}'", request.Id);
            return (null, StatusCode(StatusCodes.Status500InternalServerError, new[] { "Billing is not configured for this item" }));
        }

        var user = await userRepository.Get(authContext.UserId);
        if (user is null)
        {
            return (null, NotFound(new[] { "User not found" }));
        }

        if (string.IsNullOrEmpty(stripeOptions.Value.SecretKey))
        {
            logger.LogError("Stripe secret key is not configured; cannot change plans");
            return (null, StatusCode(StatusCodes.Status500InternalServerError, new[] { "Billing is not configured" }));
        }

        if (string.IsNullOrEmpty(user.StripeCustomerId))
        {
            return (null, BadRequest(new[] { "No active subscription to change. Subscribe to a plan first." }));
        }

        LiveSubscription subscription;
        try
        {
            subscription = await stripeGateway.GetLiveSubscription(user.StripeCustomerId);
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe rejected subscription lookup for user {UserId}: {StripeCode} {StripeMessage}",
                user.Id, ex.StripeError?.Code, ex.StripeError?.Message);
            return (null, StatusCode(StatusCodes.Status502BadGateway, new[] { $"Stripe rejected the subscription lookup: {DescribeStripeError(ex)}" }));
        }

        if (subscription is null)
        {
            return (null, BadRequest(new[] { "No active subscription to change. Subscribe to a plan first." }));
        }

        if (subscription.Status is not ("active" or "trialing"))
        {
            return (null, BadRequest(new[] { "Your last payment is outstanding. Update your payment method in the billing portal before changing plans." }));
        }

        if (subscription.CancelAtPeriodEnd)
        {
            return (null, BadRequest(new[] { "Your subscription is set to cancel at the end of the period. Reactivate it in the billing portal before changing plans." }));
        }

        if (!catalog.TryResolveTierFromPrice(subscription.PriceId, out var currentTier, out var currentInterval))
        {
            logger.LogError("Subscription {SubscriptionId} for user {UserId} is on unconfigured price {PriceId}",
                subscription.Id, user.Id, subscription.PriceId);
            return (null, StatusCode(StatusCodes.Status500InternalServerError, new[] { "Billing is not configured for your current plan" }));
        }

        var timing = BillingCatalog.ClassifyPlanChange(currentTier, currentInterval, targetTier, targetInterval);
        if (timing is null)
        {
            return (null, BadRequest(new[] { "You're already on this plan." }));
        }

        return (new PlanChange
        {
            User = user,
            Subscription = subscription,
            TargetTier = targetTier,
            TargetInterval = targetInterval,
            TargetPriceId = targetPriceId,
            Timing = timing
        }, null);
    }

    /// <summary>
    /// Stripe's own message ("No such price: 'price_x'", "Invalid API Key provided: sk_test_***")
    /// is the actionable part when a purchase fails; without it the client only sees the
    /// generic 500 from GlobalExceptionMiddleware. Stripe itself redacts key material in them.
    /// </summary>
    private static string DescribeStripeError(StripeException ex)
    {
        return string.IsNullOrEmpty(ex.StripeError?.Message) ? ex.Message : ex.StripeError.Message;
    }
}
