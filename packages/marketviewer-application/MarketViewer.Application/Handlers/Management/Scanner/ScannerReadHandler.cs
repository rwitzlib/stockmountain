using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Management;
using MarketViewer.Core.Auth;
using MarketViewer.Core.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MarketViewer.Application.Handlers.Management.Scanner;

public class ScannerReadHandler(
    AuthContext authContext,
    IScannerRepository scannerRepository,
    ILogger<ScannerReadHandler> logger)
{
    public async Task<OperationResult<ScannerResponse>> Handle(string id, CancellationToken cancellationToken)
    {
        try
        {
            var scanner = await scannerRepository.Get(id);

            if (scanner == null || scanner.UserId != authContext.UserId)
            {
                logger.LogInformation("Scanner {ScannerId} not found for user {UserId}", id, authContext.UserId);
                return new OperationResult<ScannerResponse>
                {
                    Status = HttpStatusCode.NotFound,
                    ErrorMessages = ["Scanner not found."]
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
            logger.LogError(ex, "Error retrieving scanner {ScannerId} for user {UserId}", id, authContext.UserId);
            return new OperationResult<ScannerResponse>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["An unexpected error occurred while retrieving the scanner."]
            };
        }
    }
}
