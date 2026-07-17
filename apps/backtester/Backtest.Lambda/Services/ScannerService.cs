using Amazon.S3;
using Amazon.S3.Model;
using Backtest.Lambda.Models;
using Backtest.Lambda.Utilities;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Requests.Market.Backtest;
using MarketViewer.Filters;
using MarketViewer.Filters.Expressions;
using MarketViewer.Filters.Interfaces;
using MarketViewer.Filters.Parsing;
using MarketViewer.Infrastructure.Config;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backtest.Lambda.Services;

public class ScannerService(IndicatorExpressionEngine engine, DataCache dataCache, IAmazonS3 s3, BacktestConfig config, ILogger<ScannerService> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<StrategyEntry>> GetStrategyEntries(WorkerRequest request)
    {
        List<StrategyEntry> strategyEntries = [];

        List<List<StrategyEntry>> strategyEntryLists = await ScanForEntries(request);

        var offset = TimeZoneInfo.FindSystemTimeZoneById("America/New_York").GetUtcOffset(request.Date);
        var marketOpen = new DateTimeOffset(request.Date.Year, request.Date.Month, request.Date.Day, 9, 30, 0, offset);
        var marketClose = new DateTimeOffset(request.Date.Year, request.Date.Month, request.Date.Day, 16, 0, 0, offset);
        var totalMinutes = (int)(marketClose - marketOpen).TotalMinutes;

        // Group entries by time for faster lookup
        var entryListsByTime = strategyEntryLists.Select(list => list.GroupBy(e => e.Start).ToDictionary(g => g.Key, g => g.ToList())).ToList();

        // Process per minute sequentially for determinism
        var processedTickers = new HashSet<string>();
        for (var i = 0; i < totalMinutes; i++)
        {
            var currentTime = marketOpen.AddMinutes(i);

            List<List<StrategyEntry>> entryLists = [];
            foreach (var entryListDictionary in entryListsByTime)
            {
                if (entryListDictionary.TryGetValue(currentTime, out var entries))
                {
                    entryLists.Add(entries);
                }
                else
                {
                    entryLists.Add([]);
                }
            }

            if (!entryLists.Any() || entryLists.Any(q => q.Count == 0))
            {
                continue;
            }

            var tickersLists = entryLists.Skip(1).Select(q => q.Select(e => e.Ticker).ToHashSet()).ToList();

            // Sort within the minute so entry order is stable regardless of which
            // filter list is first or how a cached (possibly unsorted) list was written.
            var validEntries = entryLists[0]
                .Where(entry => tickersLists.All(q => q.Contains(entry.Ticker)))
                .OrderBy(entry => entry.Ticker, StringComparer.Ordinal)
                .ToList();

            if (!validEntries.Any())
            {
                continue;
            }

            foreach (var entry in validEntries)
            {
                if (!request.PositionSettings.AllowSimultaneous && processedTickers.Contains(entry.Ticker))
                {
                    continue;
                }

                strategyEntries.Add(entry);
                processedTickers.Add(entry.Ticker);
            }
        }

        return strategyEntries;
    }

    public async Task<List<StrategyEntry>> GetResultsFromFilter(string filter, DateTimeOffset date)
    {
        List<StrategyEntry> strategyResults = [];

        var offset = TimeZoneInfo.FindSystemTimeZoneById("America/New_York").GetUtcOffset(date);
        var marketOpen = new DateTimeOffset(date.Year, date.Month, date.Day, 9, 30, 0, offset);
        var marketClose = new DateTimeOffset(date.Year, date.Month, date.Day, 16, 0, 0, offset);

        var expression = engine.ParseExpression(filter);
        var timeframe = engine.ExtractTimeframe(expression) ?? new Timeframe(1, Timespan.minute);
        var isScalarFilter = IsScalarOnlyFilter(expression);

        var tickers = dataCache.GetTickers();

        var sp = Stopwatch.StartNew();
        Parallel.ForEach(tickers, ticker =>
        {
            try
            {
                var session = engine.Compile(filter);
                var stocksResponse = dataCache.GetStocksResponse(ticker, timeframe)?.Clone();

                if (stocksResponse is null)
                {
                    return;
                }

                // Float (and other ticker-level scalars) don't change intraday — evaluate once.
                if (isScalarFilter)
                {
                    if (!session.Evaluate(stocksResponse, timeframe))
                    {
                        return;
                    }

                    for (int i = 0; i < (marketClose - marketOpen).TotalMinutes - 1; i++)
                    {
                        if (!dataCache.HasNextCandle(ticker, i, out _))
                        {
                            continue;
                        }

                        lock (strategyResults)
                        {
                            strategyResults.Add(new StrategyEntry
                            {
                                Ticker = ticker,
                                Start = marketOpen.AddMinutes(i),
                            });
                        }
                    }

                    return;
                }

                for (int i = 0; i < (marketClose - marketOpen).TotalMinutes - 1; i++)
                {
                    if (!dataCache.HasNextCandle(ticker, i, out var nextCandle))
                    {
                        continue;
                    }

                    stocksResponse.UpdateLatestCandle(timeframe, nextCandle);

                    var passesFilter = session.EvaluateIncremental(stocksResponse, timeframe);

                    if (!passesFilter)
                    {
                        continue;
                    }

                    lock (strategyResults)
                    {
                        strategyResults.Add(new StrategyEntry
                        {
                            Ticker = ticker,
                            Start = marketOpen.AddMinutes(i),
                        });
                    } 
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing ticker {Ticker} for filter {Filter}", ticker, filter);
                return;
            }
        });
        sp.Stop();

        // Parallel.ForEach appends in thread-scheduling order; sort so the cached
        // and returned lists are identical across runs.
        strategyResults = strategyResults
            .OrderBy(q => q.Start)
            .ThenBy(q => q.Ticker, StringComparer.Ordinal)
            .ToList();

        try
        {
            var putRequest = new PutObjectRequest
            {
                BucketName = config.S3BucketName,
                Key = BuildCacheKey(date, filter),
                ContentBody = CompressionUtilities.CompressString(JsonSerializer.Serialize(strategyResults, _jsonOptions))
            };
            await s3.PutObjectAsync(putRequest);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cache strategy results to S3 for filter: {Filter}", filter);
        }

        return strategyResults;
    }

    #region Private Methods

    private static bool IsScalarOnlyFilter(IExpression expression)
    {
        while (expression is TimeframeRangeExpression timeframeRange)
        {
            expression = timeframeRange.GetInnerExpression();
        }

        return expression switch
        {
            DataAccessExpression dataAccess => dataAccess.IsScalar,
            BinaryExpression binary => IsScalarOnlyFilter(binary.Left) || IsScalarOnlyFilter(binary.Right),
            UnaryExpression unary => IsScalarOnlyFilter(unary.Operand),
            _ => false
        };
    }
    private async Task<List<List<StrategyEntry>>> ScanForEntries(WorkerRequest request)
    {
        List<List<StrategyEntry>> strategyEntryLists = [];

        var tasks = new List<Task<(string Filter, List<StrategyEntry> Entries)>>();
        foreach (var filter in request.EntrySettings.Filters)
        {
            tasks.Add(GetResultsFromCache(filter, request));
        }
        var strategyEntryListsFromCache = (await Task.WhenAll(tasks)).ToList();

        var foundStrategyEntryLists = strategyEntryListsFromCache.Where(q => q.Entries.Any()).ToList();
        var missingStrategyEntryLists = strategyEntryListsFromCache.Where(q => !q.Entries.Any());

        foreach (var strategyEntryList in foundStrategyEntryLists)
        {
            strategyEntryLists.Add(strategyEntryList.Entries);
        }

        if (!missingStrategyEntryLists.Any())
        {
            return strategyEntryLists;
        }

        var timeframes = request.EntrySettings.Filters.Select(engine.ParseExpression)
                .Select(engine.ExtractTimeframe)
                .Where(q => q is not null)
                .DistinctBy(q => new { q.Multiplier, q.Timespan })
                .OrderBy(q => q.Multiplier)
                .OrderBy(q => q.Timespan)
                .ToList();

        // Float-only (and other scalar) filters have no timeframe; still need minute bars to scan.
        if (timeframes.Count == 0)
        {
            timeframes.Add(new Timeframe(1, Timespan.minute));
        }

        // A failed setup on a warm container leaves the previous invocation's date in the
        // cache — scanning against that produces entries for tickers that never traded
        // on this date. Fail the day loudly instead.
        if (!await dataCache.Setup(request.Date, timeframes))
        {
            throw new InvalidOperationException($"Market data setup failed for {request.Date:yyyy-MM-dd}; aborting scan to avoid using stale data.");
        }

        var sp = Stopwatch.StartNew();
        var missingStrategyEntryListTasks = new List<Task>();
        foreach (var missingStrategyEntryList in missingStrategyEntryLists)
        {
            missingStrategyEntryListTasks.Add(Task.Run(async () =>
            {
                var strategyEntries = await GetResultsFromFilter(missingStrategyEntryList.Filter, request.Date);
                lock (strategyEntryLists)
                {
                    strategyEntryLists.Add(strategyEntries);
                }
            }));
        }
        await Task.WhenAll(missingStrategyEntryListTasks);
        sp.Stop();

        return strategyEntryLists;
    }

    private async Task<(string Filter, List<StrategyEntry> Entries)> GetResultsFromCache(string filter, WorkerRequest request)
    {
        var cacheKey = BuildCacheKey(request.Date, filter);

        try
        {
            var s3Object = await s3.GetObjectAsync(config.S3BucketName, cacheKey);

            using var reader = new StreamReader(s3Object.ResponseStream);
            var content = await reader.ReadToEndAsync();

            var strategyEntries = CompressionUtilities.DecompressString<List<StrategyEntry>>(content, _jsonOptions);

            // Validate cache results are for the correct date
            if (strategyEntries.Any() && strategyEntries.First().Start.Date == request.Date.Date)
            {
                return (filter, strategyEntries);
            }
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Cache miss - continue to generate results
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve cached strategy results from S3 for filter: {Filter}", filter);
        }
        return (filter, []);
    }

    /// <summary>
    /// Filter strings contain characters that break S3 requests (&lt;, &gt;, spaces), which
    /// made every cache read/write fail. Key on a stable hash of the filter instead.
    /// </summary>
    private static string BuildCacheKey(DateTimeOffset date, string filter)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(filter)));
        return $"strategyEntries/{date:yyyy/MM/dd}/{hash[..16]}";
    }

    #endregion
}
