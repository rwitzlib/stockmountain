// using Backtest.Lambda.Models;
// using Backtest.Lambda.Services;
// using Backtest.Lambda.UnitTests.Fixtures;
// using FluentAssertions;
// using MarketViewer.Contracts.Enums;
// using MarketViewer.Contracts.Models;
// using MarketViewer.Contracts.Models.Strategy;
// using MarketViewer.Contracts.Requests.Market.Backtest;
// using Microsoft.Extensions.DependencyInjection;
//
// namespace Backtest.Lambda.UnitTests.Services;
//
// public class ScannerServiceUnitTests : IClassFixture<MarketCacheFixture>
// {
//     private readonly ScannerService _classUnderTest;
//
//     private readonly DateTimeOffset _date = DateTimeOffset.Parse("2025-05-27");
//
//     public ScannerServiceUnitTests(MarketCacheFixture fixture)
//     {
//         Skip.If(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AWS_DEPLOYMENT_ROLE")));
//
//         _classUnderTest = fixture.ServiceProvider.GetService<ScannerService>();
//         var dataCache = fixture.ServiceProvider.GetService<DataCache>();
//
//         var timeframes = new List<Timeframe>
//         {
//             new Timeframe(1, Timespan.minute),
//             new Timeframe(1, Timespan.hour),
//             new Timeframe(1, Timespan.day)
//         };
//         dataCache.Setup(_date, timeframes).ConfigureAwait(false).GetAwaiter().GetResult();
//     }
//
//     [SkippableTheory]
//     [InlineData("volume > 10000 [1m]")]
//     [InlineData("close > 505 [1m]")]
//     public async Task GetResultsFromFilter_Single_Filter(string filter)
//     {
//         // Arrange
//         var request = new WorkerRequest
//         {
//             Date = _date,
//             PositionSettings = new StrategyPositionSettings
//             {
//                 AllowSimultaneous = false
//             },
//             EntrySettings = new StrategyEntrySettings
//             {
//                 Filters =
//                 [
//                     filter
//                 ]
//             },
//             ExitSettings = new StrategyExitSettings
//             {
//                 TimedExit = new TimedExit
//                 {
//                     Timeframe = new Timeframe(5, Timespan.minute)
//                 }
//             }
//         };
//
//         // Act
//         var result = await _classUnderTest.GetResultsFromFilter(request.EntrySettings.Filters[0], request.Date);
//
//         // Assert
//         result.Count.Should().BeGreaterThan(0);
//     }
//
//     [SkippableFact]
//     public async Task GetResultsFromFilter_Multiple_Filters()
//     {
//         // Arrange
//         var request = new WorkerRequest
//         {
//             Date = _date,
//             PositionSettings = new StrategyPositionSettings
//             {
//                 AllowSimultaneous = false
//             },
//             EntrySettings = new StrategyEntrySettings
//             {
//                 Filters =
//                 [
//                     "volume > 10000 [1m, 3]",
//                     "close > 100 [1m]",
//                     "macd(12,26,9,ema) > 1 [1m]"
//                 ]
//             },
//             ExitSettings = new StrategyExitSettings
//             {
//                 TimedExit = new TimedExit
//                 {
//                     Timeframe = new Timeframe(5, Timespan.minute)
//                 }
//             }
//         };
//
//         // Act
//         var results = new List<Task<List<StrategyEntry>>>
//         {
//             Task.Run(() => _classUnderTest.GetResultsFromFilter(request.EntrySettings.Filters[0], request.Date)),
//             Task.Run(() => _classUnderTest.GetResultsFromFilter(request.EntrySettings.Filters[1], request.Date)),
//             Task.Run(() => _classUnderTest.GetResultsFromFilter(request.EntrySettings.Filters[2], request.Date))
//         };
//         var allResults = await Task.WhenAll(results);
//
//         // Assert
//         allResults[0].Count.Should().BeGreaterThan(0);
//         allResults[1].Count.Should().BeGreaterThan(0);
//         allResults[2].Count.Should().BeGreaterThan(0);
//
//         List<StrategyEntry> strategyEntries = [];
//         // we want to iterate over each minute between market open and market close and check which entries are present in all three results
//         var marketOpen = DateTimeOffset.Parse("2025-05-27T09:30:00-04:00");
//         var marketClose = DateTimeOffset.Parse("2025-05-27T16:00:00-04:00");
//         var totalMinutes = (int)(marketClose - marketOpen).TotalMinutes;
//         
//         // Group entries by time for faster lookup
//         var entriesByTime1 = allResults[0].GroupBy(q => q.Start).ToDictionary(g => g.Key, g => g.ToList());
//         var entriesByTime2 = allResults[1].GroupBy(q => q.Start).ToDictionary(g => g.Key, g => g.ToList());
//         var entriesByTime3 = allResults[2].GroupBy(q => q.Start).ToDictionary(g => g.Key, g => g.ToList());
//         List<Dictionary<DateTimeOffset, List<StrategyEntry>>> entriesByTimeList = [entriesByTime1, entriesByTime2, entriesByTime3];
//
//         // Process per minute sequentially for determinism
//         var processedTickers = new HashSet<string>();
//         for (var i = 0; i < totalMinutes; i++)
//         {
//             var currentTime = marketOpen.AddMinutes(i);
//
//             List<List<StrategyEntry>> entryLists = [];
//             foreach (var entry in entriesByTimeList)
//             {
//                 if (entry.TryGetValue(currentTime, out var entries))
//                 {
//                     entryLists.Add(entries);
//                 }
//                 else
//                 {
//                     entryLists.Add([]);
//                 }
//             }
//
//             if (!entryLists.Any() || entryLists.Any(q => q.Count == 0))
//             {
//                 continue;
//             }
//
//             // Create lookup sets for faster ticker matching
//             var tickers2 = entryLists[1].Select(e => e.Ticker).ToHashSet();
//             var tickers3 = entryLists[2].Select(e => e.Ticker).ToHashSet();
//
//             var validEntries = entryLists[0].Where(entry =>
//                 tickers2.Contains(entry.Ticker) && tickers3.Contains(entry.Ticker)).ToList();
//
//             if (validEntries.Count == 0) continue;
//
//             foreach (var entry in validEntries)
//             {
//                 if (!request.PositionSettings.AllowSimultaneous && processedTickers.Contains(entry.Ticker))
//                 {
//                     continue;
//                 }
//
//                 strategyEntries.Add(entry);
//                 processedTickers.Add(entry.Ticker);
//             }
//         }
//
//         strategyEntries.Count.Should().Be(14);
//
//         var tsla = strategyEntries.First(q => q.Ticker == "TSLA");
//         tsla.Start.Should().Be(DateTimeOffset.Parse("2025-05-27T09:34:00-04:00"));
//     }
//
//     [SkippableFact]
//     public async Task GetStrategyEntries_With_Single_Filter()
//     {
//         // Arrange
//         var request = new WorkerRequest
//         {
//             Date = _date,
//             PositionSettings = new StrategyPositionSettings
//             {
//                 AllowSimultaneous = false
//             },
//             EntrySettings = new StrategyEntrySettings
//             {
//                 Filters =
//                 [
//                     "volume > 10000000 [1m]"
//                 ]
//             },
//             ExitSettings = new StrategyExitSettings
//             {
//                 TimedExit = new TimedExit
//                 {
//                     Timeframe = new Timeframe(5, Timespan.minute)
//                 }
//             }
//         };
//
//         // Act
//         var result = await _classUnderTest.GetStrategyEntries(request);
//     
//         // Assert
//         result.Count.Should().BeGreaterThan(0);
//     }
//
//     [SkippableFact]
//     public async Task GetStrategyEntries_With_Multiple_Filters()
//     {
//         // Arrange
//         var request = new WorkerRequest
//         {
//             Date = _date,
//             PositionSettings = new StrategyPositionSettings
//             {
//                 AllowSimultaneous = false
//             },
//             EntrySettings = new StrategyEntrySettings
//             {
//                 Filters =
//                 [
//                     "volume > 10000 [1m, 3]",
//                     "close > 100 [1m]"
//                 ]
//             },
//             ExitSettings = new StrategyExitSettings
//             {
//                 TimedExit = new TimedExit
//                 {
//                     Timeframe = new Timeframe(5, Timespan.minute)
//                 }
//             }
//         };
//
//         // Act
//         var result = await _classUnderTest.GetStrategyEntries(request);
//
//         // Assert
//         result.Count.Should().BeGreaterThan(0);
//     }
//
//     [SkippableFact]
//     public async Task GetStrategyEntries_With_Multiple_Complex_Filters()
//     {
//         // Arrange
//         var request = new WorkerRequest
//         {
//             Date = _date,
//             PositionSettings = new StrategyPositionSettings
//             {
//                 AllowSimultaneous = false
//             },
//             EntrySettings = new StrategyEntrySettings
//             {
//                 Filters =
//                 [
//                     "volume > 10000 [1m, 3]",
//                     "close > 100 [1m]",
//                     "macd(12,26,9,ema) > 1 [1m]"
//                 ]
//             },
//             ExitSettings = new StrategyExitSettings
//             {
//                 TimedExit = new TimedExit
//                 {
//                     Timeframe = new Timeframe(15, Timespan.minute)
//                 }
//             }
//         };
//
//         // Act
//         var result = await _classUnderTest.GetStrategyEntries(request);
//
//         // Assert
//         result.Count.Should().Be(27);
//
//         var tsla = result.First(q => q.Ticker == "TSLA");
//         tsla.Start.Should().Be(DateTimeOffset.Parse("2025-05-27T09:34:00-04:00"));
//     }
//
//     [SkippableFact]
//     public async Task GetStrategyEntriesWithLargeTimeframe()
//     {
//         // Arrange
//         var request = new WorkerRequest
//         {
//             Date = _date,
//             PositionSettings = new StrategyPositionSettings
//             {
//                 AllowSimultaneous = false
//             },
//             EntrySettings = new StrategyEntrySettings
//             {
//                 Filters =
//                 [
//                     "close > ema(200) [1d]"
//                 ]
//             },
//             ExitSettings = new StrategyExitSettings
//             {
//                 TimedExit = new TimedExit
//                 {
//                     Timeframe = new Timeframe(15, Timespan.day)
//                 }
//             }
//         };
//
//         // Act
//         var result = await _classUnderTest.GetStrategyEntries(request);
//
//         // Assert
//         result.Count.Should().BeGreaterThan(0);
//     }
// }
