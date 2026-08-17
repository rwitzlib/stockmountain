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
using Massive.Client.Models;
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

            if (request.Filters is not { Count: > 0 })
            {
                return new OperationResult<ScanResponse>
                {
                    Status = HttpStatusCode.BadRequest,
                    ErrorMessages = ["At least one filter is required."]
                };
            }

            List<IExpression> filters;
            try
            {
                filters = request.Filters
                    .Select(engine.ParseExpression)
                    .OrderBy(q => ExpressionPlanner.Analyze(q).EstimatedCost)
                    .ToList();
            }
            catch (Exception ex)
            {
                return new OperationResult<ScanResponse>
                {
                    Status = HttpStatusCode.BadRequest,
                    ErrorMessages = [$"Invalid filter: {ex.Message}"]
                };
            }

            var tickers = marketCache.GetTickers();
            if (tickers is null)
            {
                logger.LogWarning("No tickers in market cache; cache warmup has not completed or is disabled.");
                return new OperationResult<ScanResponse>
                {
                    Status = HttpStatusCode.OK,
                    Data = new ScanResponse { TimeElapsed = sp.ElapsedMilliseconds }
                };
            }

            var timestamp = request.Timestamp ?? DateTimeOffset.Now;

            List<Task<ScanResponse.Item>> tasks = [];
            foreach (var ticker in tickers)
            {
                tasks.Add(Task.Run(() => ScanTicker(ticker, filters, timestamp, request.CompletedBarsOnly)));
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

    private ScanResponse.Item ScanTicker(string ticker, IReadOnlyList<IExpression> filters, DateTimeOffset timestamp, bool completedBarsOnly)
    {
        // Completed-bar mode drops the in-progress bar but keeps the ring buffer:
        // those are completed minutes the snapshot poll hasn't landed yet, so entries
        // still fire seconds after a minute closes (with lit-only volume until the
        // snapshot corrects it — conservative for "volume >" filters; see ADR 0003).
        var latestBar = completedBarsOnly ? null : marketCache.GetLiveBar(ticker);
        var recentBars = marketCache.GetRecentLiveBars(ticker);

        foreach (var filter in filters)
        {
            var timeframe = engine.ExtractTimeframe(filter) ?? new Timeframe(1, Timespan.minute);

            var stocksResponse = marketCache.GetStocksResponse(ticker, timeframe, timestamp);

            if (stocksResponse is null)
            {
                return null;
            }

            var clonedResponse = stocksResponse.Clone();
            AttachTickerDetails(ticker, clonedResponse);

            // TODO: add if statement to conditionally include latest bar
            TryAddBarToResponse(timeframe.Multiplier, timeframe.Timespan, latestBar, recentBars, clonedResponse);

            var passesFilter = engine.EvaluateExpression(filter, clonedResponse, timeframe, evaluationTime: timestamp);

            if (!passesFilter)
            {
                return null;
            }
        }

        var minStocksResponse = marketCache.GetStocksResponse(ticker, new Timeframe(1, Timespan.minute), timestamp);

        if (minStocksResponse?.Results is not { Count: > 0 })
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

    internal static void TryAddBarToResponse(int multiplier, Timespan timespan, Bar latestBar, IReadOnlyList<Bar> recentBars, StocksResponse response)
    {
        if (response?.Results is not { Count: > 0 })
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

                // Completed websocket bars cover the window between a minute closing
                // and the snapshot poll appending it, when the just-closed bar exists
                // nowhere else. The timestamp guards make this idempotent once the
                // snapshot bar lands.
                var lastMinute = response.Results.Last();
                if (recentBars is { Count: > 0 })
                {
                    foreach (var recentBar in recentBars)
                    {
                        if (recentBar.Timestamp > lastMinute.Timestamp
                            && (latestBar is null || recentBar.Timestamp < latestBar.Timestamp))
                        {
                            response.Results.Add(recentBar);
                            lastMinute = recentBar;
                        }
                    }
                }

                if (latestBar is not null && latestBar.Timestamp > lastMinute.Timestamp)
                {
                    response.Results.Add(latestBar);
                }
                break;
            case Timespan.hour:
                if (multiplier != 1)
                {
                    return; // Only add live bar for 1 hour aggregates
                }

                if (latestBar is null || latestBar.Timestamp <= response.Results.Last().Timestamp)
                {
                    return;
                }

                var last = response.Results.Last();

                if (latestBar.Timestamp / 3_600_000 > last.Timestamp / 3_600_000)
                {
                    response.Results.Add(latestBar);
                }
                else
                {
                    // Update the last bar with the latest data
                    last.Close = latestBar.Close;
                    last.High = Math.Max(last.High, latestBar.High);
                    last.Low = Math.Min(last.Low, latestBar.Low);
                    // Volume-weighted, from the merged bars' vw (must run before Volume is summed).
                    last.Vwap = BarVwap.Merge(last, latestBar, BarVwap.TypicalPrice(last));
                    last.Volume += latestBar.Volume;
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
