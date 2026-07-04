using MarketViewer.Api.Services;
using MarketViewer.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace MarketViewer.Api.Controllers.Auth;

[ApiController]
[AllowAnonymous]
[Route("api/webhooks/clerk")]
public class ClerkWebhookController(
    ClerkWebhookVerifier verifier,
    ClerkUserProvisioningService provisioningService,
    ILogger<ClerkWebhookController> logger) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Handle()
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync();

        if (!verifier.Verify(payload, Request.Headers))
        {
            return BadRequest("Webhook verification failed");
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var eventType = root.GetProperty("type").GetString();

        if (eventType is not ("user.created" or "user.updated"))
        {
            logger.LogDebug("Ignoring Clerk webhook event type {EventType}", eventType);
            return Ok();
        }

        var data = root.GetProperty("data");
        var userId = data.GetProperty("id").GetString();
        var imageUrl = data.TryGetProperty("image_url", out var imageUrlProperty)
            ? imageUrlProperty.GetString()
            : null;

        var provisioned = await provisioningService.Provision(new ClerkUserProfile(userId ?? string.Empty, imageUrl));
        if (!provisioned)
        {
            logger.LogError("Failed to provision StockMountain user for Clerk user {ClerkUserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to provision user");
        }

        logger.LogInformation("Provisioned StockMountain user profile for Clerk user {ClerkUserId}", userId);
        return Ok();
    }
}
