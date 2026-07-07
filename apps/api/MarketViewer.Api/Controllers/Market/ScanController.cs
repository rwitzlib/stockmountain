using System.Net;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using MarketViewer.Api.Authorization;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Requests.Market.Scan;
using MarketViewer.Contracts.Responses.Tools;
using MarketViewer.Application.Handlers.Market.Scan;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketViewer.Api.Controllers.Market;

[ApiController]
[Authorize]
[Route("/scan")]
public class TickerController(ScanHandler scanHandler, ILogger<TickerController> logger) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [RequiresTier(UserRole.Basic)]
    public async Task<IActionResult> Scan([FromBody] ScanRequest request)
    {
        var response = await scanHandler.Handle(request, HttpContext.RequestAborted);

        return response.Status switch
        {
            HttpStatusCode.OK => Ok(response.Data),
            HttpStatusCode.BadRequest => BadRequest(response.ErrorMessages),
            HttpStatusCode.NotFound => NotFound(response.ErrorMessages),
            _ => StatusCode(StatusCodes.Status500InternalServerError, response.ErrorMessages)
        };
    }
}
