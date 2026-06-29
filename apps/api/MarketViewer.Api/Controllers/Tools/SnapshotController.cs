using MarketViewer.Api.Authorization;
using MarketViewer.Application.Handlers.Tools;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Requests.Tools;
using MarketViewer.Contracts.Responses.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Net;

namespace MarketViewer.Api.Controllers.Tools;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SnapshotController(SnapshotHandler handler, ILogger<SnapshotController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [RequiredPermissions([UserRole.Admin])]
    public IActionResult Snapshot([FromQuery] SnapshotRequest request)
    {
        var response = handler.GetSnapshot(request);
        return response.Status switch
        {
            HttpStatusCode.OK => Ok(response.Data),
            HttpStatusCode.BadRequest => BadRequest(response.ErrorMessages),
            _ => StatusCode(StatusCodes.Status500InternalServerError, response.ErrorMessages)
        };
    }
}