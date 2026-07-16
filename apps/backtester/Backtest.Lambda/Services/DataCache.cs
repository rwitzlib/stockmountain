using Amazon.S3;
using Amazon.S3.Model;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.MarketData;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using Microsoft.Extensions.Logging;
using Polygon.Client.Models;
using System.Diagnostics;
using System.Text.Json;

namespace Backtest.Lambda.Services;

public class DataCache(IMarketCache marketCache, IAmazonS3 s3, ILogger<DataCache> logger)
{
    private List<string> Tickers { get; set; } = [];
    private Dictionary<string, StocksResponse> StocksResponses { get; set; } = [];
    private Dictionary<string, Bar[]> NextCandlesCache { get; set; } = [];
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<bool> Setup(DateTimeOffset date, List<Timeframe> timeframes)
    {
        try
        {
            // This cache is a singleton and its collections are keyed without a date;
            // clear them so a warm Lambda container can never scan against data left
            // over from a previous invocation's date.
            Tickers = [];
            StocksResponses = [];
            NextCandlesCache = [];

            var offset = TimeZoneInfo.FindSystemTimeZoneById("America/New_York").GetUtcOffset(date);
            var marketOpen = new DateTimeOffset(date.Year, date.Month, date.Day, 9, 30, 0, offset);
            var marketClose = new DateTimeOffset(date.Year, date.Month, date.Day, 16, 0, 0, offset);
            var totalMinutes = (int)(marketClose - marketOpen).TotalMinutes;

            // The 1-minute timeframe must be present and processed first: the candle rebuild
            // for larger timeframes below reads the cached minute response.
            var orderedTimeframes = timeframes
                .Where(q => !(q.Multiplier == 1 && q.Timespan == Timespan.minute))
                .ToList();
            orderedTimeframes.Insert(0, new Timeframe(1, Timespan.minute));

            var initializeTasks = orderedTimeframes.Select(timeframe => marketCache.Initialize(date, timeframe)).ToList();

            if (timeframes.Any(q => q.Timespan == Timespan.day))
            {
                initializeTasks.Add(marketCache.Initialize(date.AddYears(-1), new Timeframe(1, Timespan.day)));
            }

            if (timeframes.Any(q => q.Timespan == Timespan.hour))
            {
                initializeTasks.Add(marketCache.Initialize(date.AddMonths(-1), new Timeframe(1, Timespan.hour)));
            }

            // Load ticker details in parallel with aggregates so float filters can resolve.
            var tickerDetailsTask = LoadTickerDetailsAsync();
            await Task.WhenAll(initializeTasks.Cast<Task>().Append(tickerDetailsTask));

            var sp = Stopwatch.StartNew();

            if (timeframes.Any(q => q.Timespan == Timespan.day))
            {
                MergePreviousPeriod(new Timeframe(1, Timespan.day), date, date.AddYears(-1));
            }

            if (timeframes.Any(q => q.Timespan == Timespan.hour))
            {
                MergePreviousPeriod(new Timeframe(1, Timespan.hour), date, date.AddMonths(-1));
            }

            sp.Stop();

            foreach (var timeframe in orderedTimeframes)
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

                        AttachTickerDetails(stocksResponse);

                        if (timeframe.Multiplier == 1 && timeframe.Timespan == Timespan.minute)
                        {
                            var candlesByTimestamp = new Dictionary<long, Bar>();
                            foreach (var candle in stocksResponse.Results.Where(q => q.Timestamp >= marketOpen.ToUnixTimeMilliseconds()))
                            {
                                candlesByTimestamp[candle.Timestamp] = candle;
                            }

                            var nextCandles = new Bar[totalMinutes];
                            for (int i = 0; i < totalMinutes; i++)
                            {
                                candlesByTimestamp.TryGetValue(marketOpen.AddMinutes(i).ToUnixTimeMilliseconds(), out nextCandles[i]);
                            }

                            lock (NextCandlesCache)
                                NextCandlesCache[ticker] = nextCandles;
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
        return HasNextCandle(ticker, minutesAfterMarketOpen, out var nextCandle) ? nextCandle : null;
    }

    public bool HasNextCandle(string ticker, int minutesAfterMarketOpen, out Bar nextCandle)
    {
        nextCandle = null;

        if (!NextCandlesCache.TryGetValue(ticker, out var candles)
            || minutesAfterMarketOpen < 0
            || minutesAfterMarketOpen >= candles.Length)
        {
            return false;
        }

        nextCandle = candles[minutesAfterMarketOpen];
        return nextCandle is not null;
    }

    #region Private Methods

    private async Task LoadTickerDetailsAsync()
    {
        try
        {
            var s3Request = new GetObjectRequest
            {
                BucketName = Environment.GetEnvironmentVariable("MARKET_DATA_BUCKET_NAME") ?? MarketDataStorageContract.DefaultBucketName,
                Key = MarketDataStorageContract.TickerDetailsKey
            };

            using var s3Response = await s3.GetObjectAsync(s3Request);
            using var streamReader = new StreamReader(s3Response.ResponseStream);
            var json = await streamReader.ReadToEndAsync();

            var tickerDetailsList = JsonSerializer.Deserialize<IEnumerable<TickerDetails>>(json, _jsonOptions)
                ?? Enumerable.Empty<TickerDetails>();

            foreach (var tickerDetails in tickerDetailsList)
            {
                if (tickerDetails?.Ticker is null)
                {
                    continue;
                }

                marketCache.SetTickerDetails(tickerDetails);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to load ticker details; float filters may not evaluate correctly.");
        }
    }

    private void AttachTickerDetails(StocksResponse stocksResponse)
    {
        var tickerDetails = marketCache.GetTickerDetails(stocksResponse.Ticker);
        if (tickerDetails is null)
        {
            return;
        }

        stocksResponse.TickerInfo ??= new StocksResponse.Information();
        stocksResponse.TickerInfo.TickerDetails = tickerDetails;
    }

    private void MergePreviousPeriod(Timeframe timeframe, DateTimeOffset date, DateTimeOffset previousDate)
    {
        var tickers = marketCache.GetTickersByTimeframe(timeframe, date);

        if (tickers is null)
        {
            return;
        }

        Parallel.ForEach(tickers, ticker =>
        {
            var current = marketCache.GetStocksResponse(ticker, timeframe, date);
            var previous = marketCache.GetStocksResponse(ticker, timeframe, previousDate);

            if (current is null || previous is null
                || current.Results is null || !current.Results.Any()
                || previous.Results is null || !previous.Results.Any())
            {
                return;
            }

            // The cached response is shared across Setup calls; skip if already merged.
            if (current.Results.First().Timestamp <= previous.Results.Last().Timestamp)
            {
                return;
            }

            current.Results.InsertRange(0, previous.Results);
        });
    }

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
