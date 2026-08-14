using MarketViewer.Api.Authorization;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Requests.Market;
using MarketViewer.Contracts.Responses.Market;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketViewer.Api.Controllers.Market;

[ApiController]
[Route("/live")]
public class LivePricesController(IMarketCache marketCache) : ControllerBase
{
    /// <summary>
    /// Latest live price per ticker for service-to-service exit evaluation: the forming
    /// websocket bar when present, else the newest completed ring-buffer bar. Forming-bar
    /// prices are canonical — only volume undercounts intra-minute (ADR 0003) — so they
    /// are safe for price-based exits even though entries require completed bars.
    /// </summary>
    [HttpPost]
    [Route("prices")]
    [Authorize(Policy = InternalTokenRequirement.PolicyName)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GetPrices([FromBody] LivePricesRequest request)
    {
        if (request?.Tickers is not { Count: > 0 })
        {
            return BadRequest(new List<string> { "At least one ticker is required." });
        }

        var response = new LivePricesResponse();

        foreach (var ticker in request.Tickers.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            // The forming bar is mutated in place by the feed loop; read each field once.
            var formingBar = marketCache.GetLiveBar(ticker);

            if (formingBar?.Close > 0)
            {
                response.Prices.Add(new LivePrice
                {
                    Ticker = ticker,
                    Price = formingBar.Close,
                    Timestamp = formingBar.Timestamp,
                    FromFormingBar = true
                });
                continue;
            }

            // Ring bars are oldest-to-newest; the newest completed bar covers tickers
            // whose forming bar hasn't started (no trades yet this minute).
            var recentBar = marketCache.GetRecentLiveBars(ticker).LastOrDefault();

            if (recentBar?.Close > 0)
            {
                response.Prices.Add(new LivePrice
                {
                    Ticker = ticker,
                    Price = recentBar.Close,
                    Timestamp = recentBar.Timestamp,
                    FromFormingBar = false
                });
            }
        }

        return Ok(response);
    }
}
