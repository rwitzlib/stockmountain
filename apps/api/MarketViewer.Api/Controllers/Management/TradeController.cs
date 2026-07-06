using MarketViewer.Api.Authorization;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Requests.Management.Trade;
using MarketViewer.Contracts.Responses.Management;
using MarketViewer.Core.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace MarketViewer.Api.Controllers.Management;

[ApiController]
[Route("/trade")]
public class TradeController(IMediator mediator, AuthContext authContext, ILogger<TradeController> logger) : ControllerBase
{
    [HttpPost]
    [Authorize]
    [RequiresTier(UserRole.Basic)]
    public async Task<IActionResult> Open([FromBody] TradeOpenRequest request)
    {
        try
        {
            var response = await mediator.Send(request);

            return response.Status switch
            {
                HttpStatusCode.OK => Ok(response.Data),
                HttpStatusCode.BadRequest => BadRequest(response.ErrorMessages),
                _ => StatusCode(StatusCodes.Status500InternalServerError, response.ErrorMessages)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in TradeController.Open for user {UserId}", authContext.UserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new[] { "An unexpected error occurred" });
        }
    }

    [HttpGet]
    public async Task<ActionResult<TradeResponse>> List([FromQuery] TradeListRequest request)
    {
        try
        {
            var response = await mediator.Send(request);
            
            return response.Status switch
            {
                HttpStatusCode.OK => Ok(response.Data),
                HttpStatusCode.BadRequest => BadRequest(response.ErrorMessages),
                HttpStatusCode.NotFound => NotFound(response.ErrorMessages),
                _ => StatusCode(StatusCodes.Status500InternalServerError, response.ErrorMessages)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in TradeController.List for user {UserId}", authContext.UserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new[] { "An unexpected error occurred" });
        }
    }

    [HttpPut("{id}")]
    [Authorize]
    [RequiresTier(UserRole.Basic)]
    public async Task<IActionResult> Close(string id, [FromBody] TradeCloseRequest request)
    {
        try
        {
            request.TradeId = id;

            var response = await mediator.Send(request);

            return response.Status switch
            {
                HttpStatusCode.OK => Ok(response.Data),
                HttpStatusCode.BadRequest => BadRequest(response.ErrorMessages),
                HttpStatusCode.NotFound => NotFound(response.ErrorMessages),
                HttpStatusCode.Forbidden => Forbid(),
                _ => StatusCode(StatusCodes.Status500InternalServerError, response.ErrorMessages)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in TradeController.Close for trade {TradeId} by user {UserId}", id, authContext.UserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new[] { "An unexpected error occurred" });
        }
    }
}
