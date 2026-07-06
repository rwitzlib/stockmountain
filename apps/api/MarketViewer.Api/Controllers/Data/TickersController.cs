using MarketViewer.Api.Authorization;
using MarketViewer.Application.Handlers.Data.Tickers;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Requests.Data.Ticker;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace MarketViewer.Api.Controllers.Data;

[ApiController]
[Authorize]
[Route("/tickers")]
public class TickersController(TickerHandler handler, ILogger<TickersController> _logger) : ControllerBase
{
    [HttpPost]
    [Route("populate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [RequiresAdmin]
    public async Task<IActionResult> PopulateTickers([FromBody] TickerPopulateRequest request)
    {
        try
        {
            var response = await handler.Populate(request);

            return response.Status switch
            {
                HttpStatusCode.OK => Ok(response.Data),
                HttpStatusCode.BadRequest => BadRequest(response.ErrorMessages),
                HttpStatusCode.NotFound => NotFound(response.ErrorMessages),
                _ => StatusCode(StatusCodes.Status500InternalServerError, response.ErrorMessages)
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new List<string> { "Internal error." });
        }
    }
}
