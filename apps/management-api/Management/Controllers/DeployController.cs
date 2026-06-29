using Management.Requests;
using Microsoft.AspNetCore.Mvc;
using Management.Services;
using Management.Enums;
using Management.Response;
using Microsoft.AspNetCore.Authorization;

namespace Management.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DeployController(DeployService deployService, ILogger<DeployController> logger) : ControllerBase
{
    [HttpPost("start")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Start([FromBody] DeployRequest request)
    {
        try
        {
            var response = await deployService.Deploy(request);

            return response.Status switch
            {
                DeployStatus.Success => Ok(response),
                DeployStatus.Failed => BadRequest(response),
                _ => StatusCode(StatusCodes.Status500InternalServerError, response),

            };
        }
        catch (Exception ex)
        {
            logger.LogError("Exception: {ex}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new DeployResponse
            {
                Id = request.Id,
                Status = DeployStatus.Failed,
                ErrorMessages = ["Internal server error."]
            });
        }
    }

    [HttpPost("stop")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Stop([FromBody] DeployRequest request)
    {
        try
        {
            var response = await deployService.ShutDown(request);

            return response.Status switch
            {
                DeployStatus.Success => NoContent(),
                DeployStatus.Failed => BadRequest(response),
                _ => StatusCode(StatusCodes.Status500InternalServerError, response),
            };
        }
        catch (Exception ex)
        {
            logger.LogError("Exception: {ex}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new DeployResponse
            {
                Id = request.Id,
                Status = DeployStatus.Failed,
                ErrorMessages = ["Internal server error."]
            });
        }
    }
}
