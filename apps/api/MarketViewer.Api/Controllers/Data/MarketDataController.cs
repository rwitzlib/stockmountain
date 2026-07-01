using MarketViewer.Api.Authorization;
using MarketViewer.Application.Handlers.MarketData;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Requests.MarketData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace MarketViewer.Api.Controllers.Data;

[ApiController]
// [Authorize]
[Route("/market-data")]
public class MarketDataController(MarketDataHandler handler, ILogger<MarketDataController> logger) : ControllerBase
{
    [HttpGet]
    [Route("inventory")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    // [RequiredPermissions([UserRole.Admin])]
    public async Task<IActionResult> ListInventory([FromQuery] MarketDataInventoryQueryRequest request)
    {
        try
        {
            var response = await handler.ListInventory(request);

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

    [HttpGet]
    [Route("runs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    // [RequiredPermissions([UserRole.Admin])]
    public async Task<IActionResult> ListRuns([FromQuery] int limit = 50)
    {
        try
        {
            var response = await handler.ListRuns(limit);
            return Ok(response.Data);
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new List<string> { "Internal error." });
        }
    }

    [HttpPost]
    [Route("backfill")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    // [RequiredPermissions([UserRole.Admin])]
    public async Task<IActionResult> Backfill([FromBody] MarketDataBackfillRequest request)
    {
        try
        {
            var response = await handler.Backfill(request);

            return response.Status switch
            {
                HttpStatusCode.Accepted => Accepted(response.Data),
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

    [HttpPost]
    [Route("reconcile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    // [RequiredPermissions([UserRole.Admin])]
    public async Task<IActionResult> Reconcile([FromBody] MarketDataInventoryQueryRequest request)
    {
        try
        {
            var response = await handler.Reconcile(request);

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
