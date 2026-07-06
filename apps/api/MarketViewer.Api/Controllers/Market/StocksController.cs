using System.Diagnostics.Metrics;
using System.Net;
using MarketViewer.Api.Authorization;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Requests.Market;
using MarketViewer.Core.Metrics;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketViewer.Api.Controllers.Market;

[ApiController]
[Route("api/[controller]")]
public class StocksController(
    IMediator mediator,
    IHttpContextAccessor contextAccessor,
    MarketMetrics marketMetrics,
    ILogger<StocksController> logger) : ControllerBase
{
    [HttpPost]
    [RequiresTier(UserRole.Basic)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> HandleAggregateRequest([FromBody] StocksRequest request)
    {
        try
        {
            request.UserId = contextAccessor.HttpContext.Items["UserId"]?.ToString();

            var response = await mediator.Send(request);

            marketMetrics.IncrementTickerCount(request.Ticker);
            marketMetrics.IncrementTimeframe(request.Multiplier, request.Timespan);

            if (request.Indicators is not null)
            {
                foreach (var indicator in request.Indicators)
                {
                    marketMetrics.IncrementIndicator(indicator);
                }
            }
            return response.Status switch
            {
                HttpStatusCode.OK => Ok(response.Data),
                HttpStatusCode.BadRequest => BadRequest(response.ErrorMessages),
                _ => StatusCode(StatusCodes.Status500InternalServerError, response.ErrorMessages)
            };
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new List<string> { "Internal error." });
        }
    }
}