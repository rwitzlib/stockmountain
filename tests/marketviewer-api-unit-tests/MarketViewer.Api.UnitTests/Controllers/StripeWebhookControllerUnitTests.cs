using FluentAssertions;
using MarketViewer.Api.Config;
using MarketViewer.Api.Controllers.Auth;
using MarketViewer.Api.Services.Billing;
using MarketViewer.Contracts.Enums;
using MarketViewer.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace MarketViewer.Api.UnitTests.Controllers;

public class StripeWebhookControllerUnitTests
{
    private const string SigningSecret = "whsec_test_secret";

    // An event type the processor ignores, so these tests exercise only the controller's
    // verification boundary.
    private const string Payload = """
    {
      "id": "evt_1",
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

    private static StripeWebhookController CreateController(string payload, string signatureHeader, string signingSecret = SigningSecret)
    {
        var processor = new StripeWebhookProcessor(
            new Mock<IUserRepository>().Object,
            new Mock<IBillingLedgerRepository>().Object,
            new Mock<IStripeGateway>().Object,
            new BillingCatalog([], [], []),
            NullLogger<StripeWebhookProcessor>.Instance);

        var controller = new StripeWebhookController(
            processor,
            Options.Create(new StripeConfig { WebhookSigningSecret = signingSecret }),
            NullLogger<StripeWebhookController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        if (signatureHeader is not null)
        {
            httpContext.Request.Headers["Stripe-Signature"] = signatureHeader;
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static string Sign(string payload, string secret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return $"t={timestamp},v1={Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    [Fact]
    public async Task ValidSignature_Returns200()
    {
        var controller = CreateController(Payload, Sign(Payload, SigningSecret));

        var result = await controller.Handle();

        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task WrongSecret_Returns400()
    {
        var controller = CreateController(Payload, Sign(Payload, "whsec_wrong"));

        var result = await controller.Handle();

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task MissingSignatureHeader_Returns400()
    {
        var controller = CreateController(Payload, signatureHeader: null);

        var result = await controller.Handle();

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TamperedPayload_Returns400()
    {
        var signature = Sign(Payload, SigningSecret);
        var controller = CreateController(Payload.Replace("cus_1", "cus_2"), signature);

        var result = await controller.Handle();

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UnconfiguredSecret_FailsClosedWith500()
    {
        var controller = CreateController(Payload, Sign(Payload, SigningSecret), signingSecret: "");

        var result = await controller.Handle();

        result.Should().BeOfType<StatusCodeResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }
}
