using MarketViewer.Contracts.Models;
using MarketViewer.Core.Auth;
using MarketViewer.Core.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MarketViewer.Application.Handlers.Management.Scanner;

public class ScannerDeleteHandler(
    AuthContext authContext,
    IScannerRepository scannerRepository,
    ILogger<ScannerDeleteHandler> logger)
{
    public async Task<OperationResult<bool>> Handle(string id, CancellationToken cancellationToken)
    {
        try
        {
            var scanner = await scannerRepository.Get(id, cancellationToken);

            if (scanner == null || scanner.UserId != authContext.UserId)
            {
                return new OperationResult<bool>
                {
                    Status = HttpStatusCode.NotFound,
                    ErrorMessages = ["Scanner not found."]
                };
            }

            var result = await scannerRepository.Delete(id, cancellationToken);

            if (!result)
            {
                return new OperationResult<bool>
                {
                    Status = HttpStatusCode.InternalServerError,
                    ErrorMessages = ["Failed to delete scanner."]
                };
            }

            return new OperationResult<bool>
            {
                Status = HttpStatusCode.NoContent,
                Data = true
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting scanner {ScannerId} for user {UserId}", id, authContext.UserId);
            return new OperationResult<bool>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["An unexpected error occurred while deleting the scanner."]
            };
        }
    }
}
