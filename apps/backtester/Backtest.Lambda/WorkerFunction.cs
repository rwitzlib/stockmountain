using Amazon.Lambda.Core;
using Backtest.Lambda.Models;
using Backtest.Lambda.Services;
using Backtest.Lambda.Utilities;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Models.Backtest;
using MarketViewer.Contracts.Requests.Market.Backtest;
using MarketViewer.Contracts.Responses.Market.Backtest;
using MarketViewer.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polygon.Client.Interfaces;
using Polygon.Client.Models;
using Polygon.Client.Requests;
using System.Data;
using System.Diagnostics;

namespace Backtest.Lambda;

public class WorkerFunction(IServiceProvider serviceProvider)
{
    private readonly IMarketCache _marketCache = serviceProvider.GetService<IMarketCache>();
    private readonly IMemoryCache _memoryCache = serviceProvider.GetService<IMemoryCache>();
    private readonly IPolygonClient _polygonClient = serviceProvider.GetRequiredService<IPolygonClient>();

    public readonly ScannerService _scannerService = serviceProvider.GetService<ScannerService>();
    public readonly IndicatorExpressionEngine _engine = serviceProvider.GetService<IndicatorExpressionEngine>();

    private readonly ILogger<WorkerFunction> _logger = serviceProvider.GetService<ILogger<WorkerFunction>>();

    private readonly TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");


    private readonly float MEMORY_FACTOR = LambdaEnvironment.GetMemoryFactor();

    public WorkerFunction() : this(Startup.ConfigureServices()) { }

    public async Task<WorkerResponse> FunctionHandler(WorkerRequest request, ILambdaContext context)
    {
        var sp = new Stopwatch();
        sp.Start();

        try
        {
            _logger.LogInformation("Starting backtest worker for {date}", request.Date.ToString("yyyy-MM-dd"));

            var strategyEntries = await _scannerService.GetStrategyEntries(request);

            _logger.LogInformation("Found {EntryCount} entries in {ElapsedSeconds} seconds", strategyEntries.Count, sp.Elapsed.TotalSeconds);

            if (strategyEntries.Count == 0)
            {
                return new WorkerResponse
                {
                    Date = request.Date.Date,
                    CreditsUsed = MEMORY_FACTOR * (float)sp.Elapsed.TotalSeconds,
                    Results = []
                };
            }

            var backtestResults = await GetBacktestResults(strategyEntries, request);

            _logger.LogInformation("Finished computing backtest results in {ElapsedSeconds} seconds.", sp.Elapsed.TotalSeconds);

            sp.Stop();

            if (backtestResults.Count == 0)
            {
                return new WorkerResponse
                {
                    Date = request.Date.Date,
                    CreditsUsed = MEMORY_FACTOR * (float)sp.Elapsed.TotalSeconds,
                    Results = []
                };
            }

            var holdProfits = backtestResults.Where(result => result.Hold.Profit > 0).Select(result => result.Hold.Profit).ToList();
            var holdLosses = backtestResults.Where(result => result.Hold.Profit < 0).Select(result => result.Hold.Profit).ToList();

            var highProfits = backtestResults.Where(result => result.High.Profit > 0).Select(result => result.High.Profit).ToList();
            var highLosses = backtestResults.Where(result => result.High.Profit < 0).Select(result => result.High.Profit).ToList();

            return new WorkerResponse
            {
                Date = request.Date,
                CreditsUsed = MEMORY_FACTOR * (float)sp.Elapsed.TotalSeconds,
                Hold = new BacktestEntryStats
                {
                    WinRatio = holdProfits.Count + holdLosses.Count > 0 ? (float)holdProfits.Count / (holdProfits.Count + holdLosses.Count) : 0,
                    AvgLoss = holdLosses.Any() ? holdLosses.Average() : 0,
                    AvgWin = holdProfits.Any() ? holdProfits.Average() : 0,
                    BalanceChange = backtestResults.Sum(result => result.Hold.Profit)
                },
                High = new BacktestEntryStats
                {
                    WinRatio = highProfits.Count + highLosses.Count > 0 ? (float)highProfits.Count / (highProfits.Count + highLosses.Count) : 0,
                    AvgLoss = highLosses.Any() ? highLosses.Average() : 0,
                    AvgWin = highProfits.Any() ? highProfits.Average() : 0,
                    BalanceChange = backtestResults.Sum(result => result.High.Profit)
                },
                Results = backtestResults
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: {message}", ex.Message);
            _logger.LogError("Stacktrace: {stackTrace}", ex.StackTrace);
            return new WorkerResponse
            {
                Date = request.Date.Date,
                CreditsUsed = MEMORY_FACTOR * (float)sp.Elapsed.TotalSeconds,
                Results = []
            };
        }
        finally
        {
            if (_memoryCache is MemoryCache memoryCache)
            {
                _logger.LogInformation("Clearing memory cache");
                GC.Collect();
            }
        }
    }

    #region Private Methods

    private async Task<List<BacktestEntryResultCollection>> GetBacktestResults(List<StrategyEntry> scannerEntries, WorkerRequest request)
    {
        int batchSize = int.TryParse(Environment.GetEnvironmentVariable("POLYGON_BATCH_SIZE"), out var polygonBatchSize) ? polygonBatchSize : 750;
        var results = new List<BacktestEntryResultCollection>();

        for (int i = 0; i < scannerEntries.Count; i += batchSize)
        {
            var batch = scannerEntries.Skip(i).Take(batchSize);
            var tasks = batch.Select(entry => Task.Run(() => GetBacktestResult(request, entry))).ToList();
            var batchResults = (await Task.WhenAll(tasks)).Where(q => q is not null).ToList();
            results.AddRange(batchResults);
        }

        return results;
    }

    private async Task<BacktestEntryResultCollection> GetBacktestResult(WorkerRequest request, StrategyEntry entry)
    {
        try
        {
            var tickerDetails = _marketCache.GetTickerDetails(entry.Ticker);

            var entryEnd = GetStrategyEnd(entry.Start, request.ExitSettings.TimedExit.Timeframe);
            var polygonRequest = new PolygonAggregateRequest
            {
                Ticker = entry.Ticker,
                Multiplier = 1,
                Timespan = "minute",
                From = entry.Start.AddHours(-2).ToUnixTimeMilliseconds().ToString(),
                To = entryEnd.ToUnixTimeMilliseconds().ToString(),
                Limit = 50000
            };

            var polygonResponse = await _polygonClient.GetAggregates(polygonRequest);

            if (polygonResponse?.Results is null || !polygonResponse.Results.Any())
            {
                return null;
            }

            //var passesExitFiltersTimestamp = await WhenPassesExitFilters(entry, request);
            //var passesExitFiltersCandle = polygonResponse.Results.FirstOrDefault(q => q.Timestamp >= passesExitFiltersTimestamp?.ToUnixTimeMilliseconds());

            entry.Bars = polygonResponse.Results.TakeLast(polygonResponse.Results.Count() - 1);

            // Get candles between 9:30 and 3:59 EST
            List<Bar> candlesWithinMarketHours = [];
            for (int i = 0; i <= (entryEnd - entry.Start).Days; i++)
            {
                var currentDay = entry.Start.AddDays(i);

                var currentOffset = TimeZone.GetUtcOffset(currentDay);
                var marketOpen = new DateTimeOffset(currentDay.Year, currentDay.Month, currentDay.Day, 9, 30, 0, currentOffset);
                var marketClose = new DateTimeOffset(currentDay.Year, currentDay.Month, currentDay.Day, 15, 59, 0, currentOffset);

                var candles = entry.Bars.Where(candle => marketOpen.ToUnixTimeMilliseconds() <= candle.Timestamp && candle.Timestamp <= marketClose.ToUnixTimeMilliseconds());

                candlesWithinMarketHours.AddRange(candles.Where(candle => candle.Timestamp > entry.Start.ToUnixTimeMilliseconds() && candle.Timestamp <= entryEnd.ToUnixTimeMilliseconds()));
            }

            if (!candlesWithinMarketHours.Any() || request.PositionSettings.Model.Size < candlesWithinMarketHours.First().Vwap)
            {
                return null;
            }

            int shares = (int)(request.PositionSettings.Model.Size / candlesWithinMarketHours.First().Vwap);

            var entryPrice = candlesWithinMarketHours.First().Vwap;
            var entryPosition = entryPrice * shares;

            var hold = candlesWithinMarketHours.Last();
            var high = candlesWithinMarketHours.MaxBy(candle => candle.Vwap);

            var result = new BacktestEntryResultCollection
            {
                Ticker = entry.Ticker,
                StartPrice = entryPrice,
                Shares = shares,
                StartPosition = entryPosition,
                BoughtAt = entry.Start,
                Hold = new BacktestEntryResult
                {
                    StoppedOut = false,
                    EndPrice = hold.Vwap,
                    EndPosition = hold.Vwap * shares,
                    Profit = hold.Vwap * shares - entryPosition,
                    SoldAt = DateTimeOffset.FromUnixTimeMilliseconds(hold.Timestamp).ToTimezone(TimeZone),

                },
                High = new BacktestEntryResult
                {
                    StoppedOut = false,
                    EndPrice = high.Vwap,
                    EndPosition = high.Vwap * shares,
                    Profit = high.Vwap * shares - entryPosition,
                    SoldAt = DateTimeOffset.FromUnixTimeMilliseconds(high.Timestamp).ToTimezone(TimeZone)
                }
            };

            //if (request.ExitInfo.Other is not null)
            //{
            //    result.Other = passesExitFiltersTimestamp is null ? result.Hold : new BacktestEntryResult
            //    {
            //        StoppedOut = true,
            //        EndPrice = passesExitFiltersCandle.Vwap,
            //        EndPosition = passesExitFiltersCandle.Vwap * shares,
            //        Profit = passesExitFiltersCandle.Vwap * shares - entryPosition,
            //        SoldAt = passesExitFiltersTimestamp.Value
            //    };
            //}

            if (CheckTakeProfit(request, shares, entryPosition, entryPrice, candlesWithinMarketHours, out var profitTarget))
            {
                var profitTargetValue = request.ExitSettings.TakeProfit.Type switch
                {
                    ExitValueType.percent => Math.Abs(request.ExitSettings.TakeProfit.Value) / 100 * entryPosition,
                    ExitValueType.flat => Math.Abs(request.ExitSettings.TakeProfit.Value),
                    _ => throw new NotImplementedException()
                };

                result.Hold.StoppedOut = true;
                result.Hold.Profit = profitTargetValue;
                result.Hold.SoldAt = DateTimeOffset.FromUnixTimeMilliseconds(profitTarget.Timestamp).ToTimezone(TimeZone);
                result.Hold.EndPosition = result.StartPosition + profitTargetValue;
                result.Hold.EndPrice = profitTarget.Vwap;

                result.High.StoppedOut = true;
                result.High.Profit = profitTargetValue;
                result.High.SoldAt = DateTimeOffset.FromUnixTimeMilliseconds(profitTarget.Timestamp).ToTimezone(TimeZone);
                result.High.EndPosition = result.StartPosition + profitTargetValue;
                result.High.EndPrice = profitTarget.Vwap;

                //if (passesExitFiltersTimestamp is null || profitTarget.Timestamp < passesExitFiltersTimestamp.Value.ToUnixTimeMilliseconds())
                //{
                //    if (request.ExitInfo.Other is not null)
                //    {
                //        result.Other.StoppedOut = true;
                //        result.Other.Profit = profitTargetValue;
                //        result.Other.SoldAt = DateTimeOffset.FromUnixTimeMilliseconds(profitTarget.Timestamp).ToTimezone(TimeZone);
                //        result.Other.EndPosition = result.StartPosition + profitTargetValue;
                //        result.Other.EndPrice = profitTarget.Vwap;
                //    }
                //}
            }

            if (CheckStopLoss(request, shares, entryPosition, entryPrice, candlesWithinMarketHours, out var stopLoss))
            {
                var stopLossValue = request.ExitSettings.StopLoss.Type switch
                {
                    ExitValueType.percent => -Math.Abs(request.ExitSettings.StopLoss.Value) / 100 * entryPosition,
                    ExitValueType.flat => -Math.Abs(request.ExitSettings.StopLoss.Value),
                    _ => throw new NotImplementedException()
                };

                // On a same-bar tie, assume the worst case: the stop fills before the target.
                if (profitTarget is null || stopLoss.Timestamp <= profitTarget.Timestamp)
                {
                    result.Hold.StoppedOut = true;
                    result.Hold.Profit = stopLossValue;
                    result.Hold.SoldAt = DateTimeOffset.FromUnixTimeMilliseconds(stopLoss.Timestamp).ToTimezone(TimeZone);
                    result.Hold.EndPosition = result.StartPosition + stopLossValue;
                    result.Hold.EndPrice = stopLoss.Vwap;

                    result.High.StoppedOut = true;
                    result.High.Profit = stopLossValue;
                    result.High.SoldAt = DateTimeOffset.FromUnixTimeMilliseconds(stopLoss.Timestamp).ToTimezone(TimeZone);
                    result.High.EndPosition = result.StartPosition + stopLossValue;
                    result.High.EndPrice = stopLoss.Vwap;
                }

                //if (passesExitFiltersTimestamp is null || stopLoss.Timestamp < passesExitFiltersTimestamp.Value.ToUnixTimeMilliseconds())
                //{
                //    if (request.ExitInfo.Other is not null)
                //    {
                //        result.Other.StoppedOut = true;
                //        result.Other.Profit = stopLossValue;
                //        result.Other.SoldAt = DateTimeOffset.FromUnixTimeMilliseconds(stopLoss.Timestamp).ToTimezone(TimeZone);
                //        result.Other.EndPosition = result.StartPosition + stopLossValue;
                //        result.Other.EndPrice = stopLoss.Vwap;
                //    }
                //}
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error processing {ticker} at {start}: {message}", entry.Ticker, entry.Start, ex.Message);
            return null;
        }
    }

    internal static bool CheckStopLoss(WorkerRequest request, int shares, float entryPosition, float entryPrice, List<Bar> results, out Bar stopLossCandle)
    {
        var stopLoss = request.ExitSettings.StopLoss;
        stopLossCandle = null;

        if (stopLoss is null)
        {
            return false;
        }

        // A stop loss is always a loss, regardless of the sign the user entered.
        var threshold = -Math.Abs(stopLoss.Value);

        Func<Bar, float> price = stopLoss.PriceActionType switch
        {
            PriceActionType.low => bar => bar.Low,
            PriceActionType.close => bar => bar.Close,
            _ => bar => bar.Vwap
        };

        stopLossCandle = stopLoss.Type switch
        {
            ExitValueType.flat => results.FirstOrDefault(bar => price(bar) * shares - entryPosition <= threshold),
            ExitValueType.percent => results.FirstOrDefault(bar => (price(bar) - entryPrice) / entryPrice * 100 <= threshold),
            _ => null
        };

        return stopLossCandle is not null;
    }

    internal static bool CheckTakeProfit(WorkerRequest request, int shares, float entryPosition, float entryPrice, List<Bar> results, out Bar profitTargetCandle)
    {
        var takeProfit = request.ExitSettings.TakeProfit;
        profitTargetCandle = null;

        if (takeProfit is null)
        {
            return false;
        }

        var threshold = Math.Abs(takeProfit.Value);

        Func<Bar, float> price = takeProfit.PriceActionType switch
        {
            PriceActionType.high => bar => bar.High,
            PriceActionType.close => bar => bar.Close,
            _ => bar => bar.Vwap
        };

        profitTargetCandle = takeProfit.Type switch
        {
            ExitValueType.flat => results.FirstOrDefault(bar => price(bar) * shares - entryPosition >= threshold),
            ExitValueType.percent => results.FirstOrDefault(bar => (price(bar) - entryPrice) / entryPrice * 100 >= threshold),
            _ => null
        };

        return profitTargetCandle is not null;
    }

    //private async Task<DateTimeOffset?> WhenPassesExitFilters(StrategyEntry entry, WorkerRequest request)
    //{
    //    if (request.ExitSettings.ConditionalExit is null || !request.ExitSettings.ConditionalExit.Any())
    //    {
    //        return null;
    //    }

    //    var filters = request.ExitSettings.ConditionalExit.Select(_engine.ParseExpression).OrderBy(q => ExpressionPlanner.Analyze(q).EstimatedCost);
    //    var timeframes = filters.Select(_engine.ExtractTimeframe).Where(q => q is not null).Distinct().ToList();

    //    var stocksResponses = new Dictionary<Timeframe, StocksResponse>();

    //    foreach (var timeframe in timeframes)
    //    {
    //        var polygonRequest = new PolygonAggregateRequest
    //        {
    //            Ticker = entry.Ticker,
    //            Multiplier = timeframe.Multiplier,
    //            Timespan = timeframe.Timespan.ToString(),
    //            From = entry.Start.AddDays(-4).ToUnixTimeMilliseconds().ToString(),
    //            To = entry.End.ToUnixTimeMilliseconds().ToString(),
    //            Limit = 50000
    //        };

    //        var polygonResponse = await _polygonClient.GetAggregates(polygonRequest);

    //        var json = JsonSerializer.Serialize(polygonResponse);
    //        var stocksResponse = JsonSerializer.Deserialize<StocksResponse>(json, Options);

    //        stocksResponses.Add(timeframe, stocksResponse);
    //    }

    //    // TODO

    //    var days = (entry.End - entry.Start.Date).Days;

    //    for (int i = 0; i < days; i++)
    //    {
    //        var currentDay = entry.Start.Date.AddDays(i);

    //        if (currentDay.DayOfWeek == DayOfWeek.Sunday || currentDay.DayOfWeek == DayOfWeek.Saturday)
    //        {
    //            continue;
    //        }

    //        var offset = TimeZone.IsDaylightSavingTime(currentDay) ? TimeSpan.FromHours(-4) : TimeSpan.FromHours(-5);
    //        var startTime = new DateTimeOffset(currentDay.Year, currentDay.Month, currentDay.Day, 9, 30, 0, offset);

    //        // Total market minutes - 1
    //        for (int j = 0; j < 389; j += 1)
    //        {
    //            var currentTime = startTime.AddMinutes(j);

    //            if (currentTime <= entry.Start)
    //            {
    //                continue;
    //            }

    //            bool passesFilter = false;

    //            foreach (var filter in argument.Filters)
    //            {
    //                var stocksResponse = stocksResponses[filter];

    //                var item = _scannerService.ApplyFilterToStocksResponse(filter, currentTime, stocksResponse);

    //                if (item is null)
    //                {
    //                    passesFilter = false;
    //                    break;
    //                }

    //                passesFilter = true;
    //            }

    //            if (passesFilter)
    //            {
    //                return currentTime;
    //            }
    //        }
    //    }

    //    return null;
    //}

    private static DateTimeOffset GetStrategyEnd(DateTimeOffset start, Timeframe timeframe)
    {
        var sellDate = timeframe.Timespan switch
        {
            Timespan.minute => start.AddMinutes(timeframe.Multiplier),
            Timespan.hour => start.AddHours(timeframe.Multiplier),
            Timespan.day => start.AddDays(timeframe.Multiplier),
            Timespan.week => start.AddDays(timeframe.Multiplier * 7),
            Timespan.month => start.AddMonths(timeframe.Multiplier),
            _ => throw new NotImplementedException($"Unsupported timeframe: {timeframe.Timespan}")
        };

        if (sellDate.DayOfWeek == DayOfWeek.Saturday)
        {
            // If Saturday, move to Monday at Market Open
            return new DateTimeOffset(sellDate.Year, sellDate.Month, sellDate.Day, 9, 30, 0, sellDate.Offset).AddDays(2);
        }
        else if (sellDate.DayOfWeek == DayOfWeek.Sunday)
        {
            // If Sunday, move to Monday at Market Open
            return new DateTimeOffset(sellDate.Year, sellDate.Month, sellDate.Day, 9, 30, 0, sellDate.Offset).AddDays(1);
        }

        return sellDate;
    }

    #endregion
}
