using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using MarketViewer.Api.Services.Og;
using MarketViewer.Application.Handlers.Market.Backtest;
using Microsoft.AspNetCore.Mvc;

namespace MarketViewer.Api.Controllers.Market;

/// <summary>
/// Link-preview endpoints for shared backtests (plan 07's "rich crawler previews"
/// fast-follow). Caddy routes social-media crawlers hitting /share/{id} here; humans
/// keep getting the SPA. Anonymous by design, like <see cref="ShareController"/> —
/// payloads are pre-redacted, and nothing here may emit or log owner-identifying data.
/// </summary>
[ApiController]
[Route("og/share")]
public partial class ShareOgController(
    BacktestShareHandler shareHandler,
    IConfiguration configuration,
    ILogger<ShareOgController> logger) : ControllerBase
{
    [GeneratedRegex("^[A-Za-z0-9_-]{20,64}$")]
    private static partial Regex ShareIdPattern();

    /// <summary>
    /// Minimal HTML whose only job is carrying Open Graph / Twitter tags for crawlers.
    /// A meta refresh sends any human who lands here to the real share page, including
    /// when the share is missing or expired (the SPA owns that messaging).
    /// </summary>
    [HttpGet]
    [Route("{shareId}")]
    public async Task<IActionResult> GetShareOgHtml(string shareId)
    {
        if (shareId is null || !ShareIdPattern().IsMatch(shareId))
        {
            return NotFound();
        }

        var origin = PublicWebOrigin();
        var shareUrl = $"{origin}/share/{shareId}";

        var card = await LoadCard(shareId);

        string title;
        string description;
        string imageUrl;
        if (card is not null)
        {
            var net = ShareOgCardRenderer.FormatSignedPercent(card.NetReturnPct, 1);
            title = $"{card.Title} — {net} · StockMountain backtest";
            description =
                $"Profit factor {card.ProfitFactor:F2} · Sharpe {card.SharpeRatio:F2} · " +
                $"Win rate {card.WinRatioPct:F1}% · Max drawdown −{Math.Abs(card.MaxDrawdownPct):F2}%. " +
                "Backtested and shared with StockMountain. Past performance does not guarantee future results.";
            imageUrl = $"{origin}/api/og/share/{shareId}/image.png";
        }
        else
        {
            // Missing and expired are indistinguishable on purpose; fall back to the site card.
            title = "StockMountain — Prove your edge before you risk a dollar";
            description = "Backtesting, trading bots, charts, and a strategy language for retail traders.";
            imageUrl = $"{origin}/og-image.png";
        }

        var html = BuildHtml(title, description, imageUrl, shareUrl);

        Response.Headers.CacheControl = "public, max-age=3600";
        return Content(html, "text/html; charset=utf-8");
    }

    [HttpGet]
    [Route("{shareId}/image.png")]
    public async Task<IActionResult> GetShareOgImage(string shareId)
    {
        if (shareId is null || !ShareIdPattern().IsMatch(shareId))
        {
            return NotFound();
        }

        var card = await LoadCard(shareId);
        if (card is null)
        {
            return NotFound();
        }

        var png = ShareOgCardRenderer.Render(card);

        // Payloads are immutable; a stale cached image past the 30-day expiry is harmless.
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(png, "image/png");
    }

    private async Task<ShareOgCard> LoadCard(string shareId)
    {
        var response = await shareHandler.GetShareJson(shareId);
        if (response.Status != HttpStatusCode.OK || string.IsNullOrEmpty(response.Data))
        {
            return null;
        }

        try
        {
            return ShareOgCard.FromPayloadJson(response.Data);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to build OG card from share payload");
            return null;
        }
    }

    private string PublicWebOrigin()
        => (configuration["PublicWebOrigin"] ?? "https://dev.stockmountain.io").TrimEnd('/');

    private static string BuildHtml(string title, string description, string imageUrl, string shareUrl)
    {
        var t = WebUtility.HtmlEncode(title);
        var d = WebUtility.HtmlEncode(description);
        var img = WebUtility.HtmlEncode(imageUrl);
        var url = WebUtility.HtmlEncode(shareUrl);

        return new StringBuilder()
            .AppendLine("<!doctype html>")
            .AppendLine("<html lang=\"en\">")
            .AppendLine("<head>")
            .AppendLine("<meta charset=\"utf-8\">")
            .AppendLine($"<title>{t}</title>")
            .AppendLine($"<meta name=\"description\" content=\"{d}\">")
            .AppendLine("<meta property=\"og:site_name\" content=\"StockMountain\">")
            .AppendLine("<meta property=\"og:type\" content=\"website\">")
            .AppendLine($"<meta property=\"og:title\" content=\"{t}\">")
            .AppendLine($"<meta property=\"og:description\" content=\"{d}\">")
            .AppendLine($"<meta property=\"og:url\" content=\"{url}\">")
            .AppendLine($"<meta property=\"og:image\" content=\"{img}\">")
            .AppendLine("<meta property=\"og:image:width\" content=\"1200\">")
            .AppendLine("<meta property=\"og:image:height\" content=\"630\">")
            .AppendLine("<meta name=\"twitter:card\" content=\"summary_large_image\">")
            .AppendLine($"<meta name=\"twitter:title\" content=\"{t}\">")
            .AppendLine($"<meta name=\"twitter:description\" content=\"{d}\">")
            .AppendLine($"<meta name=\"twitter:image\" content=\"{img}\">")
            .AppendLine($"<meta http-equiv=\"refresh\" content=\"0;url={url}\">")
            .AppendLine("</head>")
            .AppendLine($"<body><a href=\"{url}\">View this backtest report on StockMountain</a></body>")
            .AppendLine("</html>")
            .ToString();
    }
}
