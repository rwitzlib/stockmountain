using MarketViewer.Api.Authorization;
using MarketViewer.Api.Config;
using MarketViewer.Api.Services.Billing;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Requests.Billing;
using MarketViewer.Contracts.Responses.Billing;
using MarketViewer.Core.Auth;
using MarketViewer.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

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
            if (!Enum.TryParse<UserRole>(request.Id, out var tier) || tier == UserRole.Free)
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
        var url = await stripeGateway.CreatePortalSession(user.StripeCustomerId, $"{returnUrlBase}/billing");

        return Ok(new PortalSessionResponse { Url = url });
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
}
