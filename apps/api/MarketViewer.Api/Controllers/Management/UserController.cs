using MarketViewer.Api.Authorization;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Requests.Management.User;
using MarketViewer.Core.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace MarketViewer.Api.Controllers.Management;

[ApiController]
[Route("user")]
public class UserController(IMediator mediator, AuthContext context, ILogger<UserController> logger) : ControllerBase
{

    [HttpGet]
    [Route("{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [Authorize]
    [RequiresTier(UserRole.Basic)]
    public async Task<IActionResult> Read([FromRoute] string userId)
    {
        try
        {
            var user = await mediator.Send(new UserReadRequest
            {
                UserId = userId
            });

            return user.Status switch
            {
                HttpStatusCode.OK => Ok(user.Data),
                HttpStatusCode.BadRequest => BadRequest(user.ErrorMessages),
                HttpStatusCode.NotFound => NotFound(user.ErrorMessages),
                _ => StatusCode(StatusCodes.Status500InternalServerError, user.ErrorMessages)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in UserController.Read for user {TargetUserId} by user {RequestingUserId}",
                userId, context.UserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new[] { "An unexpected error occurred" });
        }
    }
}
