using FluentValidation;
using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Requests.Management.Scanner;
using MarketViewer.Contracts.Responses.Management;
using MarketViewer.Core.Auth;
using MarketViewer.Core.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MarketViewer.Application.Handlers.Management.Scanner;

public class ScannerCreateHandler(
    AuthContext authContext,
    IScannerRepository scannerRepository,
    IValidator<ScannerCreateRequest> validator,
    ILogger<ScannerCreateHandler> logger)
{
    public async Task<OperationResult<ScannerResponse>> Handle(ScannerCreateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                logger.LogWarning("Validation failed for scanner create request: {Errors}",
                    string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
                return new OperationResult<ScannerResponse>
                {
                    Status = HttpStatusCode.BadRequest,
                    ErrorMessages = validationResult.Errors.Select(e => e.ErrorMessage).ToList()
                };
            }

            logger.LogInformation("Creating scanner '{Name}' for user {UserId}", request.Name, authContext.UserId);

            var scanner = new ScannerDto
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = authContext.UserId,
                Name = request.Name,
                EntrySettings = request.EntrySettings
            };

            var scannerDto = await scannerRepository.Create(scanner);

            if (scannerDto == null)
            {
                logger.LogError("Repository failed to create scanner '{Name}' for user {UserId}", request.Name, authContext.UserId);
                return new OperationResult<ScannerResponse>
                {
                    Status = HttpStatusCode.InternalServerError,
                    ErrorMessages = ["Failed to create scanner."]
                };
            }

            return new OperationResult<ScannerResponse>
            {
                Status = HttpStatusCode.OK,
                Data = MapToResponse(scannerDto)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating scanner '{Name}' for user {UserId}", request.Name, authContext.UserId);
            return new OperationResult<ScannerResponse>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["An unexpected error occurred while creating the scanner."]
            };
        }
    }

    internal static ScannerResponse MapToResponse(ScannerDto scanner) => new()
    {
        Id = scanner.Id,
        UserId = scanner.UserId,
        Name = scanner.Name,
        EntrySettings = scanner.EntrySettings
    };
}
