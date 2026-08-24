using MarketViewer.Api.Config;
using MarketViewer.Api.Services.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;

namespace MarketViewer.Api.Controllers.Auth;

[ApiController]
[AllowAnonymous]
[Route("webhooks/stripe")]
public class StripeWebhookController(
    StripeWebhookProcessor processor,
    IOptions<StripeConfig> stripeOptions,
    ILogger<StripeWebhookController> logger) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Handle()
    {
        var signingSecret = stripeOptions.Value.WebhookSigningSecret;
        if (string.IsNullOrEmpty(signingSecret))
        {
            // Fail closed: without the secret, no payload can be trusted.
            logger.LogError("Stripe webhook received but Stripe:WebhookSigningSecret is not configured");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        // The SDK NREs (rather than throwing StripeException) on a missing header.
        string signatureHeader = Request.Headers["Stripe-Signature"];
        if (string.IsNullOrEmpty(signatureHeader))
        {
            return BadRequest("Missing Stripe-Signature header");
        }

        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync();

        Event stripeEvent;
        try
        {
            // The dashboard's webhook API version can lag the SDK's pinned version;
            // signature verification is the trust boundary, not the version match.
            stripeEvent = EventUtility.ConstructEvent(
                payload,
                signatureHeader,
                signingSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            logger.LogWarning(ex, "Stripe webhook signature verification failed");
            return BadRequest("Webhook verification failed");
        }

        logger.LogInformation("Processing Stripe webhook event {EventId} ({EventType})", stripeEvent.Id, stripeEvent.Type);

        var handled = await processor.Process(stripeEvent);
        if (!handled)
        {
            // Non-2xx makes Stripe redeliver; the ledger keeps the retry idempotent.
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return Ok();
    }
}
