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
            await userRepository.SetStripeCustomerId(user.Id, customerId);
        }

        var returnUrlBase = stripeOptions.Value.ReturnUrlBase.TrimEnd('/');
        var url = await stripeGateway.CreateCheckoutSession(new CheckoutSessionSpec
        {
            UserId = user.Id,
            CustomerId = customerId,
            PriceId = priceId,
            IsSubscription = isSubscription,
            PackId = isSubscription ? null : request.Id,
            SuccessUrl = $"{returnUrlBase}/billing?status=success",
            CancelUrl = $"{returnUrlBase}/billing?status=cancelled"
        });

        return Ok(new CheckoutSessionResponse { Url = url });
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
            SubscriptionStatus = string.IsNullOrEmpty(user.SubscriptionStatus) ? "none" : user.SubscriptionStatus
        });
    }
}
