using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Requests.Management.Trade;
using MarketViewer.Contracts.Records;
using MarketViewer.Core.Auth;
using MarketViewer.Core.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MarketViewer.Application.Handlers.Management.Trade;

public class TradeCloseHandler(
    AuthContext authContext,
    ITradeRepository tradeRepository,
    ILogger<TradeCloseHandler> logger)
{
    public async Task<OperationResult<bool>> Handle(TradeCloseRequest request, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Closing trade {TradeId} for user {UserId} with profit {Profit}",
                request.TradeId, authContext.UserId, request.Profit);

            var existingTrade = await tradeRepository.Get(request.TradeId);

            if (existingTrade == null)
            {
                logger.LogWarning("Trade {TradeId} not found for user {UserId}", request.TradeId, authContext.UserId);
                return new OperationResult<bool>
                {
                    Status = HttpStatusCode.NotFound,
                    ErrorMessages = ["Trade not found."]
                };
            }

            if (existingTrade.UserId != authContext.UserId)
            {
                logger.LogWarning("Access denied: Trade {TradeId} belongs to user {TradeOwnerId} but requested by user {RequestingUserId}",
                    request.TradeId, existingTrade.UserId, authContext.UserId);
                return new OperationResult<bool>
                {
                    Status = HttpStatusCode.Forbidden,
                    ErrorMessages = ["Trade not found."]
                };
            }

            logger.LogDebug("Existing trade details: {@TradeDetails}", new
            {
                existingTrade.Id,
                existingTrade.Ticker,
                existingTrade.StrategyId,
                existingTrade.OrderStatus,
                existingTrade.EntryPrice,
                existingTrade.Shares
            });

            var trade = new TradeRecord
            {
                Id = request.TradeId,
                UserId = authContext.UserId,
                StrategyId = existingTrade.StrategyId,
                Ticker = existingTrade.Ticker,
                Type = existingTrade.Type,
                OrderStatus = TradeStatus.Closed,
                OpenedAt = existingTrade.OpenedAt,
                ClosedAt = DateTimeOffset.Now.ToString(),
                EntryPrice = existingTrade.EntryPrice,
                EntryPosition = existingTrade.EntryPosition,
                Shares = existingTrade.Shares,
                ClosePrice = request.ClosePrice,
                ClosePosition = request.ClosePosition,
                Profit = request.Profit
            };

            logger.LogDebug("Closing trade with details: {@CloseDetails}", new
            {
                trade.ClosePrice,
                trade.ClosePosition,
                trade.Profit,
                ClosedAt = trade.ClosedAt
            });

            var result = await tradeRepository.Put(trade);

            if (!result)
            {
                logger.LogError("Failed to persist closed trade {TradeId} for user {UserId}", request.TradeId, authContext.UserId);
                return new OperationResult<bool>
                {
                    Status = HttpStatusCode.InternalServerError,
                    ErrorMessages = ["An error occurred while closing the trade."]
                };
            }

            logger.LogInformation("Successfully closed trade {TradeId} for user {UserId} with profit {Profit}",
                request.TradeId, authContext.UserId, request.Profit);

            return new OperationResult<bool>
            {
                Status = HttpStatusCode.OK,
                Data = true
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error closing trade {TradeId} for user {UserId}", request.TradeId, authContext.UserId);
            return new OperationResult<bool>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["An unexpected error occurred while closing the trade."]
            };
        }
    }
}

