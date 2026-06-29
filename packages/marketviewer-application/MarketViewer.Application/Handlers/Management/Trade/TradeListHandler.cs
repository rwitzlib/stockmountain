using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Requests.Management.Trade;
using MarketViewer.Contracts.Responses.Management;
using MarketViewer.Core.Auth;
using MarketViewer.Core.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MarketViewer.Application.Handlers.Management.Trade;

public class TradeListHandler(
    AuthContext authContext,
    ITradeRepository tradeRepository,
    IUserRepository userRepository,
    IStrategyRepository strategyRepository,
    ILogger<TradeListHandler> logger) : IRequestHandler<TradeListRequest, OperationResult<TradeResponse>>
{
    public async Task<OperationResult<TradeResponse>> Handle(TradeListRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // TODO add fluent validation
            logger.LogInformation("Listing trades for user {UserId} with filters {@Filters}",
                authContext.UserId, new { request.User, request.Strategy, request.Type, request.Status });

            if (request.User is not null)
            {
                logger.LogDebug("Listing trades by user {TargetUserId} for requesting user {RequestingUserId}",
                    request.User, authContext.UserId);

                if (authContext.UserId != request.User)
                {
                    var user = await userRepository.Get(request.User);

                    if (user == null)
                    {
                        logger.LogWarning("User {TargetUserId} not found when listing trades", request.User);
                        return new OperationResult<TradeResponse>
                        {
                            Status = HttpStatusCode.NotFound,
                            ErrorMessages = ["User not found."]
                        };
                    }

                    if (!user.IsPublic && user.Id != authContext.UserId)
                    {
                        logger.LogWarning("Access denied: User {TargetUserId} is not public and requesting user {RequestingUserId} is not the owner",
                            request.User, authContext.UserId);
                        return new OperationResult<TradeResponse>
                        {
                            Status = HttpStatusCode.NotFound,
                            ErrorMessages = ["No trades found."]
                        };
                    }
                }

                var trades = await tradeRepository.ListTradesByUser(request.User, request.Type, request.Status);

                var response = BuildTradeResponse(trades ?? []);

                if (!response.Trades.Any())
                {
                    logger.LogInformation("No trades found for user {TargetUserId} with filters {@Filters}",
                        request.User, new { request.Type, request.Status });
                }
                else
                {
                    logger.LogInformation("Found {Count} trades for user {TargetUserId}", response.Trades.Count(), request.User);
                }

                return new OperationResult<TradeResponse>
                {
                    Status = HttpStatusCode.OK,
                    Data = response
                };
            }
            else if (request.Strategy is not null)
            {
                logger.LogDebug("Listing trades by strategy {StrategyId} for user {UserId}", request.Strategy, authContext.UserId);

                var strategy = await strategyRepository.Get(request.Strategy);

                if (strategy == null)
                {
                    logger.LogWarning("Strategy {StrategyId} not found when listing trades", request.Strategy);
                    return new OperationResult<TradeResponse>
                    {
                        Status = HttpStatusCode.NotFound,
                        ErrorMessages = ["Strategy not found."]
                    };
                }

                if (strategy.Visibility != VisibilityType.Public && strategy.UserId != authContext.UserId)
                {
                    logger.LogWarning("Access denied: Strategy {StrategyId} is not public and requesting user {UserId} is not the owner",
                        request.Strategy, authContext.UserId);
                    return new OperationResult<TradeResponse>
                    {
                        Status = HttpStatusCode.NotFound,
                        ErrorMessages = ["No trades found."]
                    };
                }

                var trades = await tradeRepository.ListTradesByStrategy(request.Strategy, request.Type, request.Status) ?? [];

                var response = BuildTradeResponse(trades);

                logger.LogInformation("Found {Count} trades for strategy {StrategyId}",
                    response.Trades.Count(), request.Strategy);

                return new OperationResult<TradeResponse>
                {
                    Status = HttpStatusCode.OK,
                    Data = response
                };
            }
            else
            {
                logger.LogWarning("Invalid trade list request: no user or strategy specified");
                return new OperationResult<TradeResponse>
                {
                    Status = HttpStatusCode.BadRequest,
                    ErrorMessages = ["Invalid request. Please provide a user or strategy."]
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing trades for user {UserId}", authContext.UserId);
            return new OperationResult<TradeResponse>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["An unexpected error occurred while listing trades."]
            };
        }
    }

    #region Private Methods

    private static TradeResponse BuildTradeResponse(IEnumerable<Contracts.Records.TradeRecord> trades)
    {
        var list = trades?.ToList() ?? [];
        var totalProfit = list.Sum(x => x.Profit);
        var avgProfit = list.Count > 0 ? totalProfit / list.Count : 0;
        var wins = list.Count > 0 ? (float)list.Count(x => x.Profit > 0) / list.Count : 0;
        var maxConcurrent = GetMaxConcurrent(list);

        return new TradeResponse
        {
            Trades = list,
            TotalTrades = list.Count,
            TotalProfit = totalProfit,
            AverageProfit = avgProfit,
            WinRate = wins,
            MaxConcurrentTrades = maxConcurrent
        };
    }

    private static int GetMaxConcurrent(List<Contracts.Records.TradeRecord> items)
    {
        if (items == null || items.Count == 0) return 0;
        var events = new List<(DateTimeOffset ts, bool open)>();
        foreach (var tr in items)
        {
            if (!string.IsNullOrEmpty(tr.OpenedAt)) events.Add((DateTimeOffset.Parse(tr.OpenedAt), true));
            if (!string.IsNullOrEmpty(tr.ClosedAt)) events.Add((DateTimeOffset.Parse(tr.ClosedAt), false));
        }
        events.Sort((a, b) =>
        {
            var cmp = a.ts.CompareTo(b.ts);
            if (cmp != 0) return cmp;
            if (a.open && !b.open) return -1;
            if (!a.open && b.open) return 1;
            return 0;
        });
        int cur = 0, max = 0;
        foreach (var e in events)
        {
            if (e.open) { cur++; max = Math.Max(max, cur); } else { cur--; }
        }
        return max;
    }

    #endregion
}

