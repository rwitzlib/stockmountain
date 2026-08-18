using System.Net;
using MarketViewer.Api.Authorization;
using MarketViewer.Application.Handlers.Market.Filters;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Requests.Market.Filters;
using Microsoft.AspNetCore.Mvc;

namespace MarketViewer.Api.Controllers.Market;

[ApiController]
[Route("filters")]
public class FiltersController(FilterValidateHandler handler) : ControllerBase
{
    [HttpPost]
    [Route("validate")]
    [RequiresTier(UserRole.Basic)]
    public IActionResult Validate([FromBody] FilterValidateRequest request)
    {
        var response = handler.Validate(request);

        return response.Status switch
        {
            HttpStatusCode.OK => Ok(response.Data),
            HttpStatusCode.BadRequest => BadRequest(response.ErrorMessages),
            _ => StatusCode(StatusCodes.Status500InternalServerError, response.ErrorMessages)
        };
    }

    [HttpGet]
    [Route("functions")]
    [RequiresTier(UserRole.Basic)]
    public IActionResult Functions([FromQuery] string? context = null)
    {
        var response = handler.Functions(context);

        return response.Status switch
        {
            HttpStatusCode.OK => Ok(response.Data),
            HttpStatusCode.BadRequest => BadRequest(response.ErrorMessages),
            _ => StatusCode(StatusCodes.Status500InternalServerError, response.ErrorMessages)
        };
    }
}
