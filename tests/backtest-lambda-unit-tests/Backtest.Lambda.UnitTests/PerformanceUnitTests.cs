// using Backtest.Lambda.Services;
// using Backtest.Lambda.UnitTests.Fixtures;
// using FluentAssertions;
// using MarketViewer.Contracts.Enums;
// using MarketViewer.Contracts.Models;
// using MarketViewer.Contracts.Models.Strategy;
// using MarketViewer.Contracts.Requests.Market.Backtest;
// using Microsoft.Extensions.DependencyInjection;
// using System.Diagnostics;
//
// namespace Backtest.Lambda.UnitTests;
//
// public class PerformanceUnitTests : IClassFixture<MarketCacheFixture>
// {
//     private readonly ScannerService _classUnderTest;
//
//     private readonly DateTimeOffset _date = DateTimeOffset.Parse("2025-05-27");
//
//     public PerformanceUnitTests(MarketCacheFixture fixture)
//     {
//         Skip.If(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AWS_DEPLOYMENT_ROLE")));
//
//         _classUnderTest = fixture.ServiceProvider.GetService<ScannerService>();
//         var timeframes = new List<Timeframe>
//         {
//             new Timeframe(1, Timespan.minute),
//         };
//         //fixture(_date, timeframes);
//         // TODO: Data setup
//     }
//
//     // [SkippableFact]
//     public async Task LiteralPerformance()
//     {
//         // Arrange
//         var sp = new Stopwatch();
//         sp.Start();
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
//                     "volume > 10001 [1m]"
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
//         sp.Stop();
//
//         // Assert
//         result.Count.Should().BeGreaterThan(0);
//     }
//
//     // [SkippableFact]
//     public async Task IndicatorPerformance()
//     {
//         // Arrange
//         var sp = new Stopwatch();
//         sp.Start();
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
//                     "macd(12,26,9,ema) > 0.1 [1m]"
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
//         sp.Stop();
//
//         // Assert
//         result.Count.Should().BeGreaterThan(0);
//     }
// }
