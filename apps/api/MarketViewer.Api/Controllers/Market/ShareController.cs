using System.Net;
using System.Text.RegularExpressions;
using MarketViewer.Application.Handlers.Market.Backtest;
using Microsoft.AspNetCore.Mvc;

namespace MarketViewer.Api.Controllers.Market;

/// <summary>
/// Anonymous read side of backtest sharing. Payloads are pre-redacted snapshots in S3;
/// this controller intentionally has no auth attributes — the share link must work for
/// viewers with no account.
/// </summary>
[ApiController]
[Route("share")]
public partial class ShareController(BacktestShareHandler shareHandler, ILogger<ShareController> logger) : ControllerBase
{
    [GeneratedRegex("^[A-Za-z0-9_-]{20,64}$")]
    private static partial Regex ShareIdPattern();

    [HttpGet]
    [Route("{shareId}")]
    public async Task<IActionResult> GetShare(string shareId)
    {
        // Reject junk before it ever becomes an S3 key (path traversal, enumeration noise).
        if (shareId is null || !ShareIdPattern().IsMatch(shareId))
        {
            return NotFound();
        }

        var response = await shareHandler.GetShareJson(shareId);

        if (response.Status == HttpStatusCode.NotFound)
        {
            logger.LogInformation("Share not found: {shareId}", shareId);
            return NotFound();
        }

        if (response.Status != HttpStatusCode.OK)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        // Payloads are immutable (re-share mints a new id), so public caching is safe.
        Response.Headers.CacheControl = "public, max-age=3600";
        return Content(response.Data, "application/json");
    }
}
