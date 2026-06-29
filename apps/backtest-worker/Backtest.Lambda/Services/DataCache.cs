using Amazon.Runtime.Internal.Util;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using Microsoft.Extensions.Logging;
using Polygon.Client.Models;
using System.Diagnostics;

namespace Backtest.Lambda.Services;

public class DataCache(IMarketCache marketCache, ILogger<DataCache> logger)
{
    private List<string> Tickers { get; set; } = [];
    private Dictionary<string, StocksResponse> StocksResponses { get; set; } = [];
    private Dictionary<string, Bar[]> NextCandlesCache { get; set; } = [];

    public async Task<bool> Setup(DateTimeOffset date, List<Timeframe> timeframes)
    {
        try
        {
            var offset = TimeZoneInfo.FindSystemTimeZoneById("America/New_York").GetUtcOffset(date);
            var marketOpen = new DateTimeOffset(date.Year, date.Month, date.Day, 9, 30, 0, offset);
            var marketClose = new DateTimeOffset(date.Year, date.Month, date.Day, 16, 0, 0, offset);
            var totalMinutes = (int)(marketClose - marketOpen).TotalMinutes;

            if (!timeframes.Any(q => q.Multiplier == 1 && q.Timespan == Timespan.minute))
            {
                timeframes.Insert(0, new Timeframe(1, Timespan.minute));
            }

            var initializeTasks = timeframes.Select(timeframe => marketCache.Initialize(date, timeframe)).ToList();

            if (timeframes.Any(q => q.Timespan == Timespan.day))
            {
                initializeTasks.Add(marketCache.Initialize(date.AddYears(-1), new Timeframe(1, Timespan.day)));
            }

            if (timeframes.Any(q => q.Timespan == Timespan.hour))
            {
                initializeTasks.Add(marketCache.Initialize(date.AddMonths(-1), new Timeframe(1, Timespan.hour)));
            }

            await Task.WhenAll(initializeTasks);

            var sp = Stopwatch.StartNew();
            var hourlyTickers = marketCache.GetTickersByTimeframe(new Timeframe(1, Timespan.day), date);

            if (hourlyTickers is not null)
            {
                Parallel.ForEach(hourlyTickers, ticker =>
                {
                    var current = marketCache.GetStocksResponse(ticker, new Timeframe(1, Timespan.day), date);
                    var previous = marketCache.GetStocksResponse(ticker, new Timeframe(1, Timespan.day), date.AddYears(-1));

                    if (current is null || previous is null
                        || current.Results is null || !current.Results.Any()
                        || previous.Results is null || !previous.Results.Any())
                    {
                        return;
                    }

                    current.Results.InsertRange(0, previous.Results);
                });
            }

            var dailyTickers = marketCache.GetTickersByTimeframe(new Timeframe(1, Timespan.day), date);

            if (dailyTickers is not null)
            {
                Parallel.ForEach(dailyTickers, ticker =>
                {
                    var current = marketCache.GetStocksResponse(ticker, new Timeframe(1, Timespan.day), date);
                    var previous = marketCache.GetStocksResponse(ticker, new Timeframe(1, Timespan.day), date.AddYears(-1));

                    if (current is null || previous is null
                        || current.Results is null || !current.Results.Any()
                        || previous.Results is null || !previous.Results.Any())
                    {
                        return;
                    }

                    current.Results.InsertRange(0, previous.Results);
                });
            }
            sp.Stop();

            var sortedTimeframes = timeframes.OrderBy(q => q.Timespan);

            foreach (var timeframe in timeframes)
            {
                var tickers = marketCache.GetTickersByTimeframe(timeframe, date);

                if (timeframe.Multiplier == 1 && timeframe.Timespan == Timespan.minute)
                {
                    Tickers = tickers.ToList();
                }

                Parallel.ForEach(tickers, ticker =>
                {
                    try
                    {
                        var stocksResponse = marketCache.GetStocksResponse(ticker, timeframe, date).Clone();

                        if (stocksResponse is null || stocksResponse.Results.Count == 0)
                        {
                            return;
                        }

                        if (timeframe.Multiplier == 1 && timeframe.Timespan == Timespan.minute)
                        {
                            var candlesAfterOpen = stocksResponse.Results.Where(candle => candle.Timestamp >= marketOpen.ToUnixTimeMilliseconds()).ToList();

                            lock (NextCandlesCache)
                                NextCandlesCache[ticker] = new Bar[totalMinutes];

                            for (int i = 0; i < totalMinutes; i++)
                            {
                                var candle = candlesAfterOpen.FirstOrDefault(q => q.Timestamp == marketOpen.AddMinutes(i).ToUnixTimeMilliseconds());

                                lock (NextCandlesCache)
                                    NextCandlesCache[ticker][i] = candle;
                            }
                        }

                        var firstMarketOpenCandle = stocksResponse.Results.FirstOrDefault(q => q.Timestamp >= marketOpen.ToUnixTimeMilliseconds());

                        if (firstMarketOpenCandle is not null)
                        {
                            var index = stocksResponse.Results.IndexOf(firstMarketOpenCandle);

                            if (index < 0)
                            {
                                return;
                            }
                            stocksResponse.Results.RemoveRange(index, stocksResponse.Results.Count - index);
                        }

                        // If timeframe would overlap with market open, we should remove that candle and all after it.  Then we should rebuild that candle from the candles up until market open.
                        if (CheckIfCurrentCandleOverlapsMarketOpen(stocksResponse, marketOpen, timeframe, out var lastCandle))
                        {
                            var startOfLastCandle = DateTimeOffset.FromUnixTimeMilliseconds(lastCandle.Timestamp);

                            var minuteStocksResponse = GetStocksResponse(ticker, new Timeframe(1, Timespan.minute));

                            if (minuteStocksResponse is null)
                            {
                                return;
                            }

                            var candles = minuteStocksResponse.Results.Where(q => q.Timestamp >= startOfLastCandle.ToUnixTimeMilliseconds() && q.Timestamp < marketOpen.ToUnixTimeMilliseconds()).ToList();

                            if (candles.Count <= 0)
                            {
                                lastCandle.Open = 0;
                                lastCandle.Close = 0;
                                lastCandle.High = 0;
                                lastCandle.Low = 0;
                                lastCandle.Vwap = 0;
                                lastCandle.Volume = 0;
                                lastCandle.TransactionCount = 0;
                                return;
                            }

                            lastCandle.Open = candles.First().Open;
                            lastCandle.Close = candles.Last().Close;
                            lastCandle.High = candles.Max(q => q.High);
                            lastCandle.Low = candles.Min(q => q.Low);
                            lastCandle.Vwap = (lastCandle.Close + lastCandle.High + lastCandle.Low) / 3; // TODO: How to do a more precise VWAP?
                            lastCandle.Volume = candles.Sum(q => q.Volume);
                            lastCandle.TransactionCount = candles.Sum(q => q.TransactionCount);
                        }

                        lock (StocksResponses)
                            StocksResponses[$"{ticker}/{timeframe.Multiplier}/{timeframe.Timespan}"] = stocksResponse;
                    }
                    catch (Exception ex)
                    {
                        // Log and continue
                        Console.WriteLine($"Error processing ticker {ticker} for timeframe {timeframe.Multiplier}/{timeframe.Timespan}: {ex.Message}");
                        return;
                    }

                });
            }
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while gathering backtest results.");
            return false;
        }
    }

    public List<string> GetTickers()
    {
        return Tickers;
    }

    public StocksResponse GetStocksResponse(string ticker, Timeframe timeframe)
    {
        if (StocksResponses.TryGetValue($"{ticker}/{timeframe.Multiplier}/{timeframe.Timespan}", out var stocksResponse))
        {
            return stocksResponse;
        }

        return null;
    }

    public Bar GetNextCandle(string ticker, int minutesAfterMarketOpen)
    {
        if (minutesAfterMarketOpen < 0 || minutesAfterMarketOpen >= NextCandlesCache[ticker].Length)
        {
            return null;
        }

        return NextCandlesCache[ticker][minutesAfterMarketOpen];
    }

    public bool HasNextCandle(string ticker, int minutesAfterMarketOpen, out Bar nextCandle)
    {
        nextCandle = null;

        if (minutesAfterMarketOpen < 0 || minutesAfterMarketOpen >= NextCandlesCache[ticker].Length)
        {
            return false;
        }

        nextCandle = NextCandlesCache[ticker][minutesAfterMarketOpen];
        return nextCandle is not null;
    }

    #region Private Methods

    public static bool CheckIfCurrentCandleOverlapsMarketOpen(StocksResponse stocksResponse, DateTimeOffset marketOpen, Timeframe timeframe, out Bar lastCandle)
    {
        lastCandle = null;
        if (stocksResponse.Results.Count <= 0)
        {
            return false;
        }

        lastCandle = stocksResponse.Results.Last();
        var lastCandleStart = DateTimeOffset.FromUnixTimeMilliseconds(lastCandle.Timestamp);

        var lastCandleEnd = timeframe.Timespan switch
        {
            Timespan.minute => lastCandleStart.AddMinutes(timeframe.Multiplier),
            Timespan.hour => lastCandleStart.AddHours(timeframe.Multiplier),
            Timespan.day => lastCandleStart.AddDays(timeframe.Multiplier),
            _ => throw new NotSupportedException($"Timespan {timeframe.Timespan} is not supported."),
        };

        return lastCandleStart < marketOpen && lastCandleEnd > marketOpen;
    }

    #endregion
}
