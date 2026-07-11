using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Net;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Requests.Market.Scan;
using MarketViewer.Contracts.Responses.Market;
using Polygon.Client.Models;
using MarketViewer.Filters;
using MarketViewer.Filters.Expressions;
using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Application.Handlers.Market.Scan;

public class ScanHandler(
    IMarketCache marketCache,
    IndicatorExpressionEngine engine,
    ILogger<ScanHandler> logger)
{
    private const int MAX_RESULTS = 1000;

    public async Task<OperationResult<ScanResponse>> Handle(ScanRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var sp = new Stopwatch();
            sp.Start();

            var tickers = marketCache.GetTickers();
            var filters = request.Filters.Select(engine.ParseExpression).OrderBy(q => ExpressionPlanner.Analyze(q).EstimatedCost);
            var timestamp = request.Timestamp ?? DateTimeOffset.Now;

            List<Task<ScanResponse.Item>> tasks = [];
            foreach (var ticker in tickers)
            {
                tasks.Add(Task.Run(() => ScanTicker(ticker, filters, timestamp)));
            }
            var items = (await Task.WhenAll(tasks)).Where(item => item is not null).ToList();

            sp.Stop();

            return new OperationResult<ScanResponse>
            {
                Status = HttpStatusCode.OK,
                Data = new ScanResponse
                {
                    Items = items.Take(MAX_RESULTS),
                    TimeElapsed = sp.ElapsedMilliseconds
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error scanning for {timestamp}: {message}", request.Timestamp, ex.Message);
            return new OperationResult<ScanResponse>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["Internal Error."]
            };
        }
    }

    #region Private Methods

    private ScanResponse.Item ScanTicker(string ticker, IOrderedEnumerable<IExpression> fitlers, DateTimeOffset timestamp)
    {
        foreach (var filter in fitlers)
        {
            var timeframe = engine.ExtractTimeframe(filter) ?? new Timeframe(1, Timespan.minute);

            var stocksResponse = marketCache.GetStocksResponse(ticker, timeframe, timestamp);

            if (stocksResponse is null)
            {
                return null;
            }

            var clonedResponse = stocksResponse.Clone();
            AttachTickerDetails(ticker, clonedResponse);
            var latestBar = marketCache.GetLiveBar(ticker);

            // TODO: add if statement to conditionally include latest bar
            TryAddBarToResponse(timeframe.Multiplier, timeframe.Timespan, latestBar, clonedResponse);

            var passesFilter = engine.EvaluateExpression(filter, clonedResponse, timeframe);

            if (!passesFilter)
            {
                return null;
            }
        }

        var minStocksResponse = marketCache.GetStocksResponse(ticker, new Timeframe(1, Timespan.minute), timestamp);

        if (minStocksResponse is null || minStocksResponse.Results?.Count == 0)
        {
            logger.LogWarning("Ticker {ticker} passed all filters but has no minute data at {timestamp}", ticker, timestamp);
            return null;
        }

        var tickerDetails = marketCache.GetTickerDetails(ticker) ?? minStocksResponse.TickerInfo?.TickerDetails;

        return new ScanResponse.Item
        {
            Ticker = ticker,
            Price = minStocksResponse.Results.Last().Close,
            Float = tickerDetails?.Float
        };
    }

    private void AttachTickerDetails(string ticker, StocksResponse stocksResponse)
    {
        var tickerDetails = marketCache.GetTickerDetails(ticker);
        if (tickerDetails is null)
        {
            return;
        }

        stocksResponse.TickerInfo ??= new StocksResponse.Information();
        stocksResponse.TickerInfo.TickerDetails = tickerDetails;
    }

    private static void TryAddBarToResponse(int multiplier, Timespan timespan, Bar latestBar, StocksResponse response)
    {
        if (latestBar is null || response is null || response.Results?.Count <= 0 || latestBar.Timestamp <= response.Results.Last().Timestamp)
        {
            return;
        }

        switch (timespan)
        {
            case Timespan.minute:
                if (multiplier != 1)
                {
                    return; // Only add live bar for 1 minute aggregates
                }
                response.Results.Add(latestBar);
                break;
            case Timespan.hour:
                if (multiplier != 1)
                {
                    return; // Only add live bar for 1 hour aggregates
                }
                var last = response.Results.Last();

                if (last.Timestamp + (60 * 60000) < latestBar.Timestamp)
                {
                    response.Results.Add(latestBar);
                }
                else
                {
                    // Update the last bar with the latest data
                    last.Close = latestBar.Close;
                    last.High = Math.Max(last.High, latestBar.High);
                    last.Low = Math.Min(last.Low, latestBar.Low);
                    last.Volume += latestBar.Volume;
                    last.Vwap = (last.Close + last.High + last.Low) / 3;
                }
                break;
            case Timespan.day:
            case Timespan.week:
            case Timespan.month:
            case Timespan.quarter:
            case Timespan.year:
                return;
            default:
                throw new NotImplementedException();
        }
    }

    #endregion
}
