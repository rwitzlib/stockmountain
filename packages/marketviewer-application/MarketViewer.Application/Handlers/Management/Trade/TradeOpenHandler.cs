using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Requests.Management.Trade;
using MarketViewer.Contracts.Records;
using MarketViewer.Core.Auth;
using MarketViewer.Core.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MarketViewer.Application.Handlers.Management.Trade;

public class TradeOpenHandler(
    AuthContext authContext,
    ITradeRepository tradeRepository,
    ILogger<TradeOpenHandler> logger) : IRequestHandler<TradeOpenRequest, OperationResult<bool>>
{
    public async Task<OperationResult<bool>> Handle(TradeOpenRequest request, CancellationToken cancellationToken)
    {
        try
        {
            //TODO fluent validation
            logger.LogInformation("Opening trade for user {UserId} with ticker {Ticker} and strategy {StrategyId}",
                authContext.UserId, request.Ticker, request.StrategyId);

            var tradeId = Guid.NewGuid().ToString();
            var tradeRecord = new TradeRecord
            {
                Id = tradeId,
                UserId = authContext.UserId,
                StrategyId = request.StrategyId,
                Ticker = request.Ticker,
                Type = request.Type,
                OrderStatus = TradeStatus.Open,
                OpenedAt = DateTimeOffset.Now.ToString(),
                EntryPrice = request.EntryPrice,
                EntryPosition = request.EntryPosition,
                Shares = request.Shares,
            };

            logger.LogDebug("Created trade record {@TradeRecord}", new
            {
                tradeRecord.Id,
                tradeRecord.UserId,
                tradeRecord.StrategyId,
                tradeRecord.Ticker,
                tradeRecord.Type,
                tradeRecord.EntryPrice,
                tradeRecord.Shares
            });

            var result = await tradeRepository.Put(tradeRecord);

            if (!result)
            {
                logger.LogError("Failed to persist trade record for user {UserId} with ticker {Ticker}",
                    authContext.UserId, request.Ticker);
                return new OperationResult<bool>
                {
                    Status = HttpStatusCode.InternalServerError,
                    ErrorMessages = ["An error occurred while opening the trade."]
                };
            }

            logger.LogInformation("Successfully opened trade {TradeId} for user {UserId} with ticker {Ticker}",
                tradeId, authContext.UserId, request.Ticker);

            return new OperationResult<bool>
            {
                Status = HttpStatusCode.OK,
                Data = true
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error opening trade for user {UserId} with ticker {Ticker}",
                authContext.UserId, request.Ticker);
            return new OperationResult<bool>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["An unexpected error occurred while opening the trade."]
            };
        }
    }
}

