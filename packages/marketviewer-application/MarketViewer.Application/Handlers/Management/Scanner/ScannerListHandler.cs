using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Management;
using MarketViewer.Core.Auth;
using MarketViewer.Core.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MarketViewer.Application.Handlers.Management.Scanner;

public class ScannerListHandler(
    AuthContext authContext,
    IScannerRepository scannerRepository,
    ILogger<ScannerListHandler> logger)
{
    public async Task<OperationResult<IEnumerable<ScannerResponse>>> Handle(CancellationToken cancellationToken)
    {
        try
        {
            var scanners = await scannerRepository.ListByUser(authContext.UserId, cancellationToken);

            if (scanners == null)
            {
                return new OperationResult<IEnumerable<ScannerResponse>>
                {
                    Status = HttpStatusCode.InternalServerError,
                    ErrorMessages = ["Failed to list scanners."]
                };
            }

            logger.LogInformation("Retrieved {Count} scanners for user {UserId}", scanners.Count(), authContext.UserId);

            return new OperationResult<IEnumerable<ScannerResponse>>
            {
                Status = HttpStatusCode.OK,
                Data = scanners.Select(ScannerCreateHandler.MapToResponse).ToList()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list scanners for user {UserId}", authContext.UserId);
            return new OperationResult<IEnumerable<ScannerResponse>>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["Failed to list scanners."]
            };
        }
    }
}
