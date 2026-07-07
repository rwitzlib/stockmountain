using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Requests.Management.Strategy;
using MarketViewer.Contracts.Responses.Management;
using MarketViewer.Core.Auth;
using MarketViewer.Core.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MarketViewer.Application.Handlers.Management.Strategy;

public class StrategyReadHandler(
    AuthContext authContext,
    IStrategyRepository strategyRepository,
    ILogger<StrategyReadHandler> logger)
{
    public async Task<OperationResult<StrategyResponse>> Handle(StrategyReadRequest request, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogDebug("Retrieving strategy {StrategyId} for user {UserId}", request.Id, authContext.UserId);

            var strategy = await strategyRepository.Get(request.Id);

            if (strategy == null)
            {
                logger.LogInformation("Strategy {StrategyId} not found", request.Id);
                return new OperationResult<StrategyResponse>
                {
                    Status = HttpStatusCode.NotFound,
                    ErrorMessages = ["Strategy not found."]
                };
            }

            if (strategy.UserId != authContext.UserId && strategy.Visibility != VisibilityType.Public)
            {
                logger.LogWarning("Access denied: Strategy {StrategyId} belongs to user {OwnerId} and is not public, requested by user {RequestingUserId}",
                    request.Id, strategy.UserId, authContext.UserId);
                return new OperationResult<StrategyResponse>
                {
                    Status = HttpStatusCode.NotFound,
                    ErrorMessages = ["Strategy not found."]
                };
            }

            var response = new StrategyResponse
            {
                Id = strategy.Id,
                Name = strategy.Name,
                Integration = strategy.Integration,
                Type = strategy.Type,
                Visibility = strategy.Visibility,
                State = strategy.State,
                PositionSettings = strategy.PositionSettings,
                ExitSettings = strategy.ExitSettings,
                EntrySettings = strategy.EntrySettings,
            };

            logger.LogInformation("Successfully retrieved strategy '{Name}' ({StrategyId}) for user {UserId}",
                strategy.Name, request.Id, authContext.UserId);

            return new OperationResult<StrategyResponse>
            {
                Status = HttpStatusCode.OK,
                Data = response,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving strategy {StrategyId} for user {UserId}", request.Id, authContext.UserId);
            return new OperationResult<StrategyResponse>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["An unexpected error occurred while retrieving the strategy."]
            };
        }
    }
}
