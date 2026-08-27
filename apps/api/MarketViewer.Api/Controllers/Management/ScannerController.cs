using MarketViewer.Api.Authorization;
using MarketViewer.Application.Handlers.Management.Scanner;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Requests.Management.Scanner;
using MarketViewer.Contracts.Responses.Management;
using MarketViewer.Core.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace MarketViewer.Api.Controllers.Management;

[ApiController]
[Authorize]
[Route("/scanner")]
public class ScannerController(
    ScannerCreateHandler createHandler,
    ScannerReadHandler readHandler,
    ScannerListHandler listHandler,
    ScannerUpdateHandler updateHandler,
    ScannerDeleteHandler deleteHandler,
    AuthContext authContext,
    ILogger<ScannerController> logger) : ControllerBase
{
    [HttpPost]
    [RequiresTier(UserRole.Pro)]
    public async Task<ActionResult<ScannerResponse>> Create(ScannerCreateRequest request)
    {
        try
        {
            var scanner = await createHandler.Handle(request, HttpContext.RequestAborted);

            return scanner.Status switch
            {
                HttpStatusCode.OK => CreatedAtAction(nameof(Get), new { id = scanner.Data.Id }, scanner.Data),
                HttpStatusCode.BadRequest => BadRequest(scanner.ErrorMessages),
                HttpStatusCode.NotFound => NotFound(scanner.ErrorMessages),
                _ => StatusCode(StatusCodes.Status500InternalServerError, scanner.ErrorMessages)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in ScannerController.Create for user {UserId}", authContext.UserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new[] { "An unexpected error occurred" });
        }
    }

    [HttpGet("{id}")]
    [RequiresTier(UserRole.Pro)]
    public async Task<ActionResult<ScannerResponse>> Get(string id)
    {
        try
        {
            var scanner = await readHandler.Handle(id, HttpContext.RequestAborted);

            return scanner.Status switch
            {
                HttpStatusCode.OK => Ok(scanner.Data),
                HttpStatusCode.BadRequest => BadRequest(scanner.ErrorMessages),
                HttpStatusCode.NotFound => NotFound(scanner.ErrorMessages),
                _ => StatusCode(StatusCodes.Status500InternalServerError, scanner.ErrorMessages)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in ScannerController.Get for user {UserId}", authContext.UserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new[] { "An unexpected error occurred" });
        }
    }

    [HttpGet]
    [RequiresTier(UserRole.Pro)]
    public async Task<ActionResult<IEnumerable<ScannerResponse>>> List()
    {
        try
        {
            var scanners = await listHandler.Handle(HttpContext.RequestAborted);

            return scanners.Status switch
            {
                HttpStatusCode.OK => Ok(scanners.Data),
                _ => StatusCode(StatusCodes.Status500InternalServerError, scanners.ErrorMessages)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in ScannerController.List for user {UserId}", authContext.UserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new[] { "An unexpected error occurred" });
        }
    }

    [HttpPut("{id}")]
    [RequiresTier(UserRole.Pro)]
    public async Task<ActionResult<ScannerResponse>> Update(string id, ScannerUpdateRequest request)
    {
        try
        {
            request.Id = id;
            var scanner = await updateHandler.Handle(request, HttpContext.RequestAborted);

            return scanner.Status switch
            {
                HttpStatusCode.OK => Ok(scanner.Data),
                HttpStatusCode.BadRequest => BadRequest(scanner.ErrorMessages),
                HttpStatusCode.NotFound => NotFound(scanner.ErrorMessages),
                _ => StatusCode(StatusCodes.Status500InternalServerError, scanner.ErrorMessages)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in ScannerController.Update for user {UserId}", authContext.UserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new[] { "An unexpected error occurred" });
        }
    }

    [HttpDelete("{id}")]
    [RequiresTier(UserRole.Pro)]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            var result = await deleteHandler.Handle(id, HttpContext.RequestAborted);

            return result.Status switch
            {
                HttpStatusCode.NoContent => NoContent(),
                HttpStatusCode.NotFound => NotFound(result.ErrorMessages),
                _ => StatusCode(StatusCodes.Status500InternalServerError, result.ErrorMessages)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in ScannerController.Delete for user {UserId}", authContext.UserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new[] { "An unexpected error occurred" });
        }
    }
}
