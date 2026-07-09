using System.Net;
using MarketViewer.Api.Authorization;
using MarketViewer.Application.Handlers.Market.Backtest;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Requests.Market.Backtest;
using MarketViewer.Core.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketViewer.Api.Controllers.Market;

[ApiController]
[Route("backtest")]

public class BacktestController(AuthContext authContext, BacktestHandler handler, ILogger<BacktestController> logger) : ControllerBase
{
    [HttpPost]
    [RequiresTier(UserRole.Basic)]
    public async Task<IActionResult> StartBacktest([FromBody] BacktestCreateRequest request)
    {
        var response = await handler.Create(request);

        return response.Status switch
        {
            HttpStatusCode.OK => Ok(response.Data),
            HttpStatusCode.BadRequest => BadRequest(response.ErrorMessages),
            _ => StatusCode(StatusCodes.Status500InternalServerError, response.ErrorMessages)
        };
    }

    [HttpGet]
    [Route("{id}")]
    [RequiresTier(UserRole.Basic)]
    public async Task<IActionResult> GetBacktestEntry(string id)
    {
        var response = await handler.GetEntry(id);

        return response.Status switch
        {
            HttpStatusCode.OK => Ok(response.Data),
            HttpStatusCode.BadRequest => BadRequest(response.ErrorMessages),
            _ => StatusCode(StatusCodes.Status500InternalServerError, response.ErrorMessages)
        };
    }

    [HttpGet]
    [RequiresTier(UserRole.Basic)]
    public async Task<IActionResult> ListBacktestEntries()
    {
        var userId = authContext.UserId;

        var response = await handler.List(userId);

        return response.Status switch
        {
            HttpStatusCode.OK => Ok(response.Data),
            HttpStatusCode.BadRequest => BadRequest(response.ErrorMessages),
            _ => StatusCode(StatusCodes.Status500InternalServerError, response.ErrorMessages)
        };
    }

    [HttpGet]
    [Route("result/{id}")]
    [RequiresTier(UserRole.Basic)]
    public async Task<IActionResult> GetBacktestResult(string id)
    {
        var response = await handler.GetResult(id);

        return response.Status switch
        {
            HttpStatusCode.OK => Ok(response.Data),
            HttpStatusCode.BadRequest => BadRequest(response.ErrorMessages),
            HttpStatusCode.NotFound => NotFound(response.ErrorMessages),
            _ => StatusCode(StatusCodes.Status500InternalServerError, response.ErrorMessages)
        };
    }

    /// <summary>
    /// Stub: unconstrained trade universe for pattern exploration (not implemented yet).
    /// </summary>
    [HttpGet]
    [Route("universe/{id}")]
    [RequiresTier(UserRole.Basic)]
    public async Task<IActionResult> GetBacktestUniverse(string id)
    {
        var response = await handler.GetUniverse(id);

        return response.Status switch
        {
            HttpStatusCode.OK => Ok(response.Data),
            HttpStatusCode.NotImplemented => StatusCode(StatusCodes.Status501NotImplemented, response.ErrorMessages),
            HttpStatusCode.BadRequest => BadRequest(response.ErrorMessages),
            HttpStatusCode.NotFound => NotFound(response.ErrorMessages),
            _ => StatusCode(StatusCodes.Status500InternalServerError, response.ErrorMessages)
        };
    }
}
