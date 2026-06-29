using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Requests.Management.Strategy;
using MarketViewer.Contracts.Responses.Management;
using MarketViewer.Core.Auth;
using MarketViewer.Core.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MarketViewer.Application.Handlers.Management.Strategy;

public class BalanceHistoryHandler(
    AuthContext authContext,
    IStrategyRepository strategyRepository,
    ILogger<BalanceHistoryHandler> logger) : IRequestHandler<BalanceHistoryRequest, OperationResult<BalanceHistoryResponse>>
{
    public async Task<OperationResult<BalanceHistoryResponse>> Handle(BalanceHistoryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogDebug("Getting balance history for strategy {StrategyId} by user {UserId}",
                request.StrategyId, authContext.UserId);

            // First verify the strategy exists and user has access
            var strategy = await strategyRepository.Get(request.StrategyId);
            if (strategy == null)
            {
                return new OperationResult<BalanceHistoryResponse>
                {
                    Status = HttpStatusCode.NotFound,
                    ErrorMessages = ["Strategy not found."]
                };
            }

            // Check ownership
            if (strategy.UserId != authContext.UserId)
            {
                logger.LogWarning("User {UserId} attempted to access balance history for strategy {StrategyId} owned by {OwnerId}",
                    authContext.UserId, request.StrategyId, strategy.UserId);
                return new OperationResult<BalanceHistoryResponse>
                {
                    Status = HttpStatusCode.NotFound,
                    ErrorMessages = ["Strategy not found."]
                };
            }

            // Default date range: 30 days
            var endDate = string.IsNullOrEmpty(request.EndDate) 
                ? DateTimeOffset.UtcNow.ToString("yyyy-MM-dd")
                : request.EndDate;
            
            var startDate = string.IsNullOrEmpty(request.StartDate)
                ? DateTimeOffset.UtcNow.AddDays(-30).ToString("yyyy-MM-dd")
                : request.StartDate;

            // Get balance history
            var history = await strategyRepository.GetBalanceHistory(request.StrategyId, startDate, endDate);

            var response = new BalanceHistoryResponse
            {
                StrategyId = request.StrategyId,
                History = history.Select(h => new BalanceHistoryEntry
                {
                    Date = h.Date,
                    CashBalance = h.CashBalance,
                    TotalEntryCost = h.TotalEntryCost,
                    UnrealizedPnl = h.UnrealizedPnl,
                    PositionValue = h.PositionValue,
                    CurrentBalance = h.CurrentBalance,
                    OpenPositionsCount = h.OpenPositionsCount,
                    RecordedAt = h.RecordedAt,
                    SnapshotType = h.SnapshotType
                }).ToList()
            };

            logger.LogInformation("Retrieved {Count} balance history entries for strategy {StrategyId}",
                response.History.Count, request.StrategyId);

            return new OperationResult<BalanceHistoryResponse>
            {
                Status = HttpStatusCode.OK,
                Data = response
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting balance history for strategy {StrategyId}", request.StrategyId);
            return new OperationResult<BalanceHistoryResponse>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["An unexpected error occurred."]
            };
        }
    }
}

