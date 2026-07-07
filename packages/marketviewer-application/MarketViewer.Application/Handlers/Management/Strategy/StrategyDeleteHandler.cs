using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Requests.Management.Strategy;
using MarketViewer.Core.Auth;
using MarketViewer.Core.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MarketViewer.Application.Handlers.Management.Strategy;

public class StrategyDeleteHandler(
    AuthContext authContext,
    IStrategyRepository strategyRepository,
    ILogger<StrategyCreateHandler> logger)
{
    public async Task<OperationResult<bool>> Handle(StrategyDeleteRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var strategy = await strategyRepository.Get(request.Id);

            if (strategy == null || strategy.UserId != authContext.UserId)
            {
                return new OperationResult<bool>
                {
                    Status = HttpStatusCode.NotFound,
                    ErrorMessages = ["Strategy not found."]
                };
            }

            if (strategy.Expiry is not null)
            {
                return new OperationResult<bool>
                {
                    Status = HttpStatusCode.NoContent,
                    Data = true
                };
            }

            var result = await strategyRepository.Delete(strategy);

            if (!result)
            {
                return new OperationResult<bool>
                {
                    Status = HttpStatusCode.InternalServerError,
                    ErrorMessages = ["Failed to delete strategy."]
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
            logger.LogError(ex, "Error deleting strategy {StrategyId} for user {UserId}", request.Id, authContext.UserId);
            return new OperationResult<bool>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["An unexpected error occurred while deleting the strategy."]
            };
        }

    }
}
