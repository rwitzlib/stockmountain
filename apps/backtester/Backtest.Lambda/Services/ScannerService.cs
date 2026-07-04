using Amazon.S3;
using Amazon.S3.Model;
using Backtest.Lambda.Models;
using Backtest.Lambda.Utilities;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Requests.Market.Backtest;
using MarketViewer.Filters;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backtest.Lambda.Services;

public class ScannerService(IndicatorExpressionEngine engine, DataCache dataCache, IAmazonS3 s3, ILogger<ScannerService> logger)
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

            var validEntries = entryLists[0].Where(entry => tickersLists.All(q => q.Contains(entry.Ticker))).ToList();

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

        try
        {
            var putRequest = new PutObjectRequest
            {
                BucketName = "lad-dev-marketviewer",
                Key = $"strategyEntries/{date:yyyy/MM/dd}/{filter}",
                ContentBody = CompressionUtilities.CompressString(JsonSerializer.Serialize(strategyResults, _jsonOptions))
            };
            await s3.PutObjectAsync(putRequest);
        }
        catch (Exception)
        {
            logger.LogError("Failed to cache strategy results to S3 for filter: {Filter}", filter);
        }

        return strategyResults;
    }

    #region Private Methods

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

        await dataCache.Setup(request.Date, timeframes);

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
        var cacheKey = $"strategyEntries/{request.Date:yyyy/MM/dd}/{filter}";

        try
        {
            var s3Object = await s3.GetObjectAsync("lad-dev-marketviewer", cacheKey);

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
        catch (Exception)
        {
            logger.LogError("Failed to retrieve cached strategy results from S3 for filter: {Filter}", filter);
        }
        return (filter, []);
    }

    #endregion
}
