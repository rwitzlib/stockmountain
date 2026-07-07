using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Requests.Management.Strategy;
using MarketViewer.Contracts.Responses.Management;
using MarketViewer.Core.Auth;
using MarketViewer.Core.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MarketViewer.Application.Handlers.Management.Strategy;

public class StrategyStateHandler(
    AuthContext authContext,
    IStrategyRepository strategyRepository,
    ILogger<StrategyStateHandler> logger)
{
    public async Task<OperationResult<StrategyStateResponse>> Handle(StrategyStateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogDebug("Getting state for strategy {StrategyId} by user {UserId}", 
                request.StrategyId, authContext.UserId);

            // First verify the strategy exists and user has access
            var strategy = await strategyRepository.Get(request.StrategyId);
            if (strategy == null)
            {
                return new OperationResult<StrategyStateResponse>
                {
                    Status = HttpStatusCode.NotFound,
                    ErrorMessages = ["Strategy not found."]
                };
            }

            // Check ownership
            if (strategy.UserId != authContext.UserId)
            {
                logger.LogWarning("User {UserId} attempted to access state for strategy {StrategyId} owned by {OwnerId}",
                    authContext.UserId, request.StrategyId, strategy.UserId);
                return new OperationResult<StrategyStateResponse>
                {
                    Status = HttpStatusCode.NotFound,
                    ErrorMessages = ["Strategy not found."]
                };
            }

            // Get the state
            var state = await strategyRepository.GetState(request.StrategyId);

            if (state == null)
            {
                // No state yet - return default with starting balance
                var startingBalance = (decimal)strategy.PositionSettings.StartingBalance;
                return new OperationResult<StrategyStateResponse>
                {
                    Status = HttpStatusCode.OK,
                    Data = new StrategyStateResponse
                    {
                        StrategyId = request.StrategyId,
                        CashBalance = startingBalance,
                        TotalEntryCost = 0,
                        UnrealizedPnl = 0,
                        PositionValue = 0,
                        CurrentBalance = startingBalance,
                        OpenPositionsCount = 0,
                        OpenTickers = [],
                        Cooldowns = [],
                        LastTradeAt = 0,
                        Version = 0
                    }
                };
            }

            return new OperationResult<StrategyStateResponse>
            {
                Status = HttpStatusCode.OK,
                Data = new StrategyStateResponse
                {
                    StrategyId = state.StrategyId,
                    CashBalance = state.CashBalance,
                    TotalEntryCost = state.TotalEntryCost,
                    UnrealizedPnl = state.UnrealizedPnl,
                    PositionValue = state.PositionValue,
                    CurrentBalance = state.CurrentBalance,
                    OpenPositionsCount = state.OpenPositionsCount,
                    OpenTickers = state.OpenTickers?.ToList() ?? [],
                    Cooldowns = state.Cooldowns ?? [],
                    LastTradeAt = state.LastTradeAt,
                    Version = state.Version
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting state for strategy {StrategyId}", request.StrategyId);
            return new OperationResult<StrategyStateResponse>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["An unexpected error occurred."]
            };
        }
    }
}

