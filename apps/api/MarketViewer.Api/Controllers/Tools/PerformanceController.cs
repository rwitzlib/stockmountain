using MarketViewer.Api.Authorization;
using MarketViewer.Application.Handlers.Tools;
using MarketViewer.Contracts.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketViewer.Api.Controllers.Tools;

[ApiController]
[Authorize]
[Route("api/performance")]
public class PerformanceController(
    PerformanceHandler handler,
    ILogger<PerformanceController> logger) : ControllerBase
{
    [HttpGet]
    [Route("{type}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [RequiredPermissions([UserRole.Admin])]
    public async Task<IActionResult> Calculate(string type)
    {
        var count = 100;
        Parallel.ForEach(Enumerable.Range(0, count), async i =>
        {
            await handler.Handle(type);
        });

        return Ok();
    }
}
