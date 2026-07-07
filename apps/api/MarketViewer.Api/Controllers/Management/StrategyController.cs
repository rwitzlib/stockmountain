using MarketViewer.Api.Authorization;
using MarketViewer.Application.Handlers.Management.Strategy;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Requests.Management.Strategy;
using MarketViewer.Contracts.Responses.Management;
using MarketViewer.Core.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace MarketViewer.Api.Controllers.Management;

[ApiController]
[Route("/strategy")]
public class StrategyController(
    StrategyCreateHandler createHandler,
    StrategyReadHandler readHandler,
    StrategyListHandler listHandler,
    StrategyUpdateHandler updateHandler,
    StrategyDeleteHandler deleteHandler,
    StrategyOptimizeHandler optimizeHandler,
    StrategyStateHandler stateHandler,
    BalanceHistoryHandler balanceHistoryHandler,
    AuthContext authContext,
    ILogger<StrategyController> logger) : ControllerBase
{
    [HttpPost]
    [Authorize]
    [RequiresTier(UserRole.Basic)]
    public async Task<ActionResult<StrategyResponse>> Create(StrategyCreateRequest request)
    {
        try
        {
            var strategy = await createHandler.Handle(request, HttpContext.RequestAborted);

            return strategy.Status switch
            {
                HttpStatusCode.OK => CreatedAtAction(nameof(Get), new { id = strategy.Data.Id }, strategy.Data),
                HttpStatusCode.BadRequest => BadRequest(strategy.ErrorMessages),
                HttpStatusCode.NotFound => NotFound(strategy.ErrorMessages),
                _ => StatusCode(StatusCodes.Status500InternalServerError, strategy.ErrorMessages)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in StrategyController.Create for user {UserId}", authContext.UserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new[] { "An unexpected error occurred" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<StrategyResponse>> Get(string id)
    {
        try
        {
            var strategy = await readHandler.Handle(new StrategyReadRequest
            {
                Id = id
            }, HttpContext.RequestAborted);

            return strategy.Status switch
            {
                HttpStatusCode.OK => Ok(strategy.Data),
                HttpStatusCode.BadRequest => BadRequest(strategy.ErrorMessages),
                HttpStatusCode.NotFound => NotFound(strategy.ErrorMessages),
                _ => StatusCode(StatusCodes.Status500InternalServerError, strategy.ErrorMessages)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in StrategyController.Get for strategy {StrategyId} by user {UserId}", id, authContext.UserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new[] { "An unexpected error occurred" });
        }
    }
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<StrategyResponse>>> List([FromQuery] StrategyListRequest request)
    {
        try
        {
            var strategies = await listHandler.Handle(request, HttpContext.RequestAborted);

            return strategies.Status switch
            {
                HttpStatusCode.OK => Ok(strategies.Data),
                HttpStatusCode.BadRequest => BadRequest(strategies.ErrorMessages),
                HttpStatusCode.NotFound => NotFound(strategies.ErrorMessages),
                _ => StatusCode(StatusCodes.Status500InternalServerError, strategies.ErrorMessages)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in StrategyController.List for user {UserId}", authContext.UserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new[] { "An unexpected error occurred" });
        }
    }

    [HttpPut("{id}")]
    [Authorize]
    [RequiresTier(UserRole.Basic)]
    public async Task<IActionResult> Update(string id, StrategyUpdateRequest request)
    {
        try
        {
            request.Id = id;

            var strategy = await updateHandler.Handle(request, HttpContext.RequestAborted);

            return strategy.Status switch
            {
                HttpStatusCode.OK => Ok(strategy.Data),
                HttpStatusCode.BadRequest => BadRequest(strategy.ErrorMessages),
                HttpStatusCode.NotFound => NotFound(strategy.ErrorMessages),
                _ => StatusCode(StatusCodes.Status500InternalServerError, strategy.ErrorMessages)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in StrategyController.Update for strategy {StrategyId} by user {UserId}", id, authContext.UserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new[] { "An unexpected error occurred" });
        }
    }

    [HttpDelete("{id}")]
    [Authorize]
    [RequiresTier(UserRole.Basic)]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            var result = await deleteHandler.Handle(new StrategyDeleteRequest
            {
                Id = id
            }, HttpContext.RequestAborted);

            return result.Status switch
            {
                HttpStatusCode.NoContent => NoContent(),
                HttpStatusCode.NotFound => NotFound("Strategy not found."),
                _ => BadRequest(result.ErrorMessages)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in StrategyController.Delete for strategy {StrategyId} by user {UserId}", id, authContext.UserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new[] { "An unexpected error occurred" });
        }
    }

    [HttpPost("optimize/{id}")]
    [Authorize]
    [RequiresTier(UserRole.Basic)]
    public async Task<IActionResult> Optimize(string id, [FromBody] StrategyOptimizeRequest request)
    {
        try
        {
            request.StrategyId = id;

            var response = await optimizeHandler.Handle(request, HttpContext.RequestAborted);

            var statusCode = response.Status switch
            {
                HttpStatusCode.OK => StatusCodes.Status200OK,
                HttpStatusCode.BadRequest => StatusCodes.Status400BadRequest,
                HttpStatusCode.Forbidden => StatusCodes.Status403Forbidden,
                HttpStatusCode.NotFound => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status500InternalServerError
            };

            return response.Status switch
            {
                HttpStatusCode.OK => Ok(response.Data),
                HttpStatusCode.BadRequest => BadRequest(response.ErrorMessages),
                HttpStatusCode.Forbidden => Forbid(),
                HttpStatusCode.NotFound => NotFound(response.ErrorMessages),
                _ => StatusCode(StatusCodes.Status500InternalServerError, response.ErrorMessages)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in StrategyController.Optimize for user {UserId}", authContext.UserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new[] { "An unexpected error occurred" });
        }
    }

    /// <summary>
    /// Gets the current state for a strategy including balance, open positions, and cooldowns.
    /// </summary>
    [HttpGet("{id}/state")]
    [Authorize]
    [RequiresTier(UserRole.Basic)]
    public async Task<ActionResult<StrategyStateResponse>> GetState(string id)
    {
        try
        {
            var response = await stateHandler.Handle(new StrategyStateRequest { StrategyId = id }, HttpContext.RequestAborted);

            return response.Status switch
            {
                HttpStatusCode.OK => Ok(response.Data),
                HttpStatusCode.NotFound => NotFound(response.ErrorMessages),
                _ => StatusCode(StatusCodes.Status500InternalServerError, response.ErrorMessages)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in StrategyController.GetState for strategy {StrategyId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new[] { "An unexpected error occurred" });
        }
    }

    /// <summary>
    /// Gets the balance history for a strategy within a date range.
    /// </summary>
    [HttpGet("{id}/balance-history")]
    [Authorize]
    [RequiresTier(UserRole.Basic)]
    public async Task<ActionResult<BalanceHistoryResponse>> GetBalanceHistory(
        string id,
        [FromQuery] string startDate = null,
        [FromQuery] string endDate = null)
    {
        try
        {
            var response = await balanceHistoryHandler.Handle(new BalanceHistoryRequest
            {
                StrategyId = id,
                StartDate = startDate,
                EndDate = endDate
            }, HttpContext.RequestAborted);

            return response.Status switch
            {
                HttpStatusCode.OK => Ok(response.Data),
                HttpStatusCode.NotFound => NotFound(response.ErrorMessages),
                _ => StatusCode(StatusCodes.Status500InternalServerError, response.ErrorMessages)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in StrategyController.GetBalanceHistory for strategy {StrategyId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new[] { "An unexpected error occurred" });
        }
    }
}