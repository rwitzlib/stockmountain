using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Interfaces;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Records;
using MarketViewer.Contracts.Requests.Management.Strategy;
using MarketViewer.Contracts.Requests.Market;
using MarketViewer.Contracts.Responses.Management;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Core.Auth;
using MarketViewer.Core.Services;
using MarketViewer.Filters;
using MarketViewer.Filters.Interfaces;
using Microsoft.Extensions.Logging;
using Massive.Client.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MarketViewer.Application.Handlers.Management.Strategy;

public class StrategyOptimizeHandler(
    AuthContext authContext,
    IStrategyRepository strategyRepository,
    ITradeRepository tradeRepository,
    IMarketDataRepository marketDataRepository,
    IMassiveClient massiveClient,
    IndicatorExpressionEngine engine,
    ILogger<StrategyCreateHandler> logger)
{
    public async Task<OperationResult<TradeResponse>> Handle(StrategyOptimizeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.StrategyId))
        {
            return new OperationResult<TradeResponse>
            {
                Status = HttpStatusCode.BadRequest,
                ErrorMessages = ["StrategyId is required."]
            };
        }

        var strategy = await strategyRepository.Get(request.StrategyId);

        if (strategy == null)
        {
            logger.LogDebug("Strategy {StrategyId} not found", request.StrategyId);
            return new OperationResult<TradeResponse>
            {
                Status = HttpStatusCode.NotFound,
                ErrorMessages = ["Strategy not found."]
            };
        }

        if (strategy.Visibility != VisibilityType.Public && strategy.UserId != authContext.UserId)
        {
            logger.LogWarning("Access denied: Strategy {StrategyId} belongs to user {OwnerId} and is not public, requested by user {RequestingUserId}",
                request.StrategyId, strategy.UserId, authContext.UserId);
            return new OperationResult<TradeResponse>
            {
                Status = HttpStatusCode.Forbidden,
                ErrorMessages = ["Access denied."]
            };
        }

        var trades = await tradeRepository.ListTradesByStrategy(request.StrategyId, request.Type, request.Status) ?? [];

        if (!trades.Any())
        {
            logger.LogDebug("No trades found for strategy {StrategyId}", request.StrategyId);
            return new OperationResult<TradeResponse>
            {
                Status = HttpStatusCode.OK,
                Data = new TradeResponse
                {
                    Trades = [],
                    TotalTrades = 0,
                    TotalProfit = 0,
                    AverageProfit = 0,
                    WinRate = 0,
                    MaxConcurrentTrades = 0
                }
            };
        }

        logger.LogInformation("Applying {FilterCount} filters to {TradeCount} trades for strategy {StrategyId}",
            request.Filters.Count, trades.Count(), request.StrategyId);

        List<TradeRecord> filteredTrades = trades.ToList();
        foreach (var filter in request.Filters ?? [])
        {
            List<Task<TradeRecord>> tasks = [];
            var expression = engine.ParseExpression(filter);

            foreach (var trade in filteredTrades)
            {
                tasks.Add(Task.Run(() => CheckIfPassesFilter(trade, expression)));
            }

            filteredTrades = (await Task.WhenAll(tasks)).Where(q => q is not null).ToList();
        }

        if (!filteredTrades.Any())
        {
            logger.LogDebug("No trades passed the filters for strategy {StrategyId}", request.StrategyId);
            return new OperationResult<TradeResponse>
            {
                Status = HttpStatusCode.OK,
                Data = new TradeResponse
                {
                    Trades = [],
                    TotalTrades = 0,
                    TotalProfit = 0,
                    AverageProfit = 0,
                    WinRate = 0,
                    MaxConcurrentTrades = 0
                }
            };
        }

        // Metrics
        var list = filteredTrades.ToList();
        var totalProfit = list.Sum(x => x.Profit);
        var avgProfit = list.Count > 0 ? totalProfit / list.Count : 0;
        var wins = list.Count > 0 ? (float)list.Count(x => x.Profit > 0) / list.Count : 0;
        var maxConcurrent = GetMaxConcurrent(list);

        var response = new TradeResponse
        {
            Trades = list,
            TotalTrades = list.Count,
            TotalProfit = totalProfit,
            AverageProfit = avgProfit,
            WinRate = wins,
            MaxConcurrentTrades = maxConcurrent
        };

        return new OperationResult<TradeResponse>
        {
            Status = HttpStatusCode.OK,
            Data = response
        };
    }

    #region Private Methods

    private static int GetMaxConcurrent(List<TradeRecord> items)
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

    private async Task<TradeRecord> CheckIfPassesFilter(TradeRecord trade, IExpression expression)
    {
        var stocksResponse = await marketDataRepository.GetStockDataAsync(new StocksRequest
        {
            Ticker = trade.Ticker,
            Multiplier = 1,
            Timespan = Timespan.day,
            From = DateTimeOffset.Parse(trade.OpenedAt).AddDays(-90),
            To = DateTimeOffset.Parse(trade.OpenedAt).AddDays(1),
            Limit = 5000
        });

        var tickerDetails = await massiveClient.GetTickerDetails(trade.Ticker);

        stocksResponse.TickerInfo = new StocksResponse.Information
        {
            TickerDetails = tickerDetails.TickerDetails
        };

        var result = engine.EvaluateExpression(expression, stocksResponse, new Timeframe(1, Timespan.day));

        if (!result)
        {
            return null;
        }

        return trade;
    }

    #endregion
}
