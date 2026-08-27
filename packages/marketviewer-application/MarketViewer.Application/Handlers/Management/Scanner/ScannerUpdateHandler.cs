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

public class ScannerUpdateHandler(
    AuthContext authContext,
    IScannerRepository scannerRepository,
    IValidator<ScannerUpdateRequest> validator,
    ILogger<ScannerUpdateHandler> logger)
{
    public async Task<OperationResult<ScannerResponse>> Handle(ScannerUpdateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                logger.LogWarning("Validation failed for scanner update request: {Errors}",
                    string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
                return new OperationResult<ScannerResponse>
                {
                    Status = HttpStatusCode.BadRequest,
                    ErrorMessages = validationResult.Errors.Select(e => e.ErrorMessage).ToList()
                };
            }

            var existingScanner = await scannerRepository.Get(request.Id, cancellationToken);

            if (existingScanner == null || existingScanner.UserId != authContext.UserId)
            {
                return new OperationResult<ScannerResponse>
                {
                    Status = HttpStatusCode.NotFound,
                    ErrorMessages = ["Scanner not found."]
                };
            }

            var updatedScanner = new ScannerDto
            {
                Id = request.Id,
                UserId = authContext.UserId,
                Name = request.Name.Trim(),
                EntrySettings = request.EntrySettings
            };

            var scanner = await scannerRepository.Update(updatedScanner, cancellationToken);

            if (scanner == null)
            {
                return new OperationResult<ScannerResponse>
                {
                    Status = HttpStatusCode.InternalServerError,
                    ErrorMessages = ["Failed to update scanner."]
                };
            }

            return new OperationResult<ScannerResponse>
            {
                Status = HttpStatusCode.OK,
                Data = ScannerCreateHandler.MapToResponse(scanner)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update scanner for user {UserId}", authContext.UserId);
            return new OperationResult<ScannerResponse>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["Failed to update scanner."]
            };
        }
    }
}
