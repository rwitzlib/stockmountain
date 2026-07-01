// using Amazon;
// using Amazon.Lambda.TestUtilities;
// using Amazon.SimpleSystemsManagement;
// using Amazon.SimpleSystemsManagement.Model;
// using FluentAssertions;
// using MarketViewer.Contracts.Enums;
// using MarketViewer.Contracts.Models;
// using MarketViewer.Contracts.Models.Strategy;
// using MarketViewer.Contracts.Requests.Market.Backtest;
// using System.Net;
// using Environment = System.Environment;
//
// namespace Backtest.Lambda.UnitTests;
//
// public class WorkerUnitTests
// {
//     private readonly TestLambdaContext _context = new TestLambdaContext();
//     private readonly WorkerFunction _classUnderTest;
//
//     private readonly DateTimeOffset _date = DateTimeOffset.Parse("2025-05-27");
//
//     public WorkerUnitTests()
//     {
//         Skip.If(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AWS_DEPLOYMENT_ROLE")));
//
//         Environment.SetEnvironmentVariable("MEMORY", "2048");
//
//         using var client = new AmazonSimpleSystemsManagementClient(RegionEndpoint.USEast2);
//         var response = client.GetParameterAsync(new GetParameterRequest
//         {
//             Name = "/tokens/polygon",
//             WithDecryption = true
//         }).Result;
//
//         if (response.HttpStatusCode == HttpStatusCode.OK)
//         {
//             Environment.SetEnvironmentVariable("POLYGON_TOKEN", response.Parameter.Value);
//         }
//
//         _classUnderTest = new WorkerFunction();
//     }
//
//     [SkippableFact]
//     public async Task SingleFilter()
//     {
//         // Arrange
//         var request = new WorkerRequest
//         {
//             Date = _date,
//             PositionSettings = new StrategyPositionSettings
//             {
//                 AllowSimultaneous = false,
//                 MaxConcurrentPositions = 1000,
//                 Model = new PositionModel
//                 {
//                     Type = PositionType.Fixed,
//                     Size = 1000
//                 },
//                 Cooldown = new Timeframe(15, Timespan.minute),
//             },
//             EntrySettings = new StrategyEntrySettings
//             {
//                 Filters =
//                 [
//                     "close > 505 [1m]"
//                 ]
//             },
//             ExitSettings = new StrategyExitSettings
//             {
//                 TakeProfit = new Exit
//                 {
//                     CandleType = ExitCandleType.PreviousCandle,
//                     Type = ExitValueType.percent,
//                     Value = 10,
//                     PriceActionType = PriceActionType.close
//                 },
//                 StopLoss = new Exit
//                 {
//                     CandleType = ExitCandleType.PreviousCandle,
//                     Type = ExitValueType.percent,
//                     Value = -10,
//                     PriceActionType = PriceActionType.close
//                 },
//                 TimedExit = new TimedExit
//                 {
//                     Timeframe = new Timeframe(5, Timespan.minute)
//                 }
//             }
//         };
//
//         // Act
//         var response = await _classUnderTest.FunctionHandler(request, _context);
//
//         // Assert
//         response.Results.Count.Should().Be(39);
//         response.Hold.WinRatio.Should().BeApproximately(.12f, .01f);
//         response.High.WinRatio.Should().BeApproximately(.30f, 0.1f);
//     }
//
//     [SkippableFact]
//     public async Task MultipleFilters()
//     {
//         // Arrange
//         var request = new WorkerRequest
//         {
//             Date = _date,
//             PositionSettings = new StrategyPositionSettings
//             {
//                 AllowSimultaneous = false,
//                 MaxConcurrentPositions = 1000,
//                 Model = new PositionModel
//                 {
//                     Type = PositionType.Fixed,
//                     Size = 1000
//                 },
//                 Cooldown = new Timeframe(15, Timespan.minute),
//             },
//             EntrySettings = new StrategyEntrySettings
//             {
//                 Filters =
//                 [
//                     "adv() > 1000000 [1d]",
//                     "close > 20 [1m]",
//                     "macd(12,26,9,ema) < -1 [1m]"
//                 ]
//             },
//             ExitSettings = new StrategyExitSettings
//             {
//                 TakeProfit = new Exit
//                 {
//                     CandleType = ExitCandleType.PreviousCandle,
//                     Type = ExitValueType.percent,
//                     Value = 10,
//                     PriceActionType = PriceActionType.close
//                 },
//                 StopLoss = new Exit
//                 {
//                     CandleType = ExitCandleType.PreviousCandle,
//                     Type = ExitValueType.percent,
//                     Value = -10,
//                     PriceActionType = PriceActionType.close
//                 },
//                 TimedExit = new TimedExit
//                 {
//                     Timeframe = new Timeframe(5, Timespan.minute)
//                 }
//             }
//         };
//
//         // Act
//         var response = await _classUnderTest.FunctionHandler(request, _context);
//
//         // Assert
//         response.Results.Count.Should().Be(21);
//         response.Hold.WinRatio.Should().BeApproximately(.42f, .01f);
//         response.High.WinRatio.Should().BeApproximately(.71f, 0.1f);
//     }
//
//     [SkippableFact]
//     public async Task Test_Result_Accuracy()
//     {
//         // Arrange
//         var request = new WorkerRequest
//         {
//             Date = _date,
//             PositionSettings = new StrategyPositionSettings
//             {
//                 AllowSimultaneous = false,
//                 MaxConcurrentPositions = 1000,
//                 Model = new PositionModel
//                 {
//                     Type = PositionType.Fixed,
//                     Size = 1000
//                 },
//                 Cooldown = new Timeframe(15, Timespan.minute),
//             },
//             EntrySettings = new StrategyEntrySettings
//             {
//                 Filters =
//                 [
//                     "close > 20 [1m]",
//                     "adv() > 1000000 [1d]",
//                     "macd(12,26,9,ema) < 0",
//                 ]
//             },
//             ExitSettings = new StrategyExitSettings
//             {
//                 TakeProfit = new Exit
//                 {
//                     CandleType = ExitCandleType.PreviousCandle,
//                     Type = ExitValueType.percent,
//                     Value = 10,
//                     PriceActionType = PriceActionType.close
//                 },
//                 StopLoss = new Exit
//                 {
//                     CandleType = ExitCandleType.PreviousCandle,
//                     Type = ExitValueType.percent,
//                     Value = -10,
//                     PriceActionType = PriceActionType.close
//                 },
//                 TimedExit = new TimedExit
//                 {
//                     Timeframe = new Timeframe(5, Timespan.minute)
//                 }
//             }
//         };
//
//         // Act
//         var response = await _classUnderTest.FunctionHandler(request, _context);
//
//         // Assert
//         response.Results.Count.Should().Be(896);
//         response.Hold.WinRatio.Should().BeApproximately(.51f, .01f);
//         response.High.WinRatio.Should().BeApproximately(.67f, 0.1f); 
//         
//         var tsla = response.Results.FirstOrDefault(q => q.Ticker == "TSLA");
//         tsla.BoughtAt.Should().Be(DateTimeOffset.Parse("2025-05-27T10:46:00-04:00"));
//     }
//
//     [SkippableFact]
//     public async Task Test_Support_Resistance()
//     {
//         // Arrange
//         var request = new WorkerRequest
//         {
//             Date = _date,
//             PositionSettings = new StrategyPositionSettings
//             {
//                 AllowSimultaneous = false,
//                 MaxConcurrentPositions = 1000,
//                 Model = new PositionModel
//                 {
//                     Type = PositionType.Fixed,
//                     Size = 1000
//                 },
//                 Cooldown = new Timeframe(15, Timespan.minute),
//             },
//             EntrySettings = new StrategyEntrySettings
//             {
//                 Filters =
//                 [
//                     //"adv() > 1000000 [1d]",
//                     "close > 2 [1m]",
//                     "close < 50 [1m]",
//                     "close > ema(50) [1h]",
//                     "close > ema(200) [1h]",
//                     //"slope(ema(50)) > 0 [1h]",
//                     "macd(12,26,9,ema) < 0 [1h]",
//                     "support_resistance().support_distance_pct < 0.8 [1h]",
//                     "support_resistance().support_touches >= 5 [1h]",
//                     "support_resistance().support_strength >= 0.65 [1h]",
//                     "support_resistance(200,3,0.6).near_support == 1 [1h]"
//                 ]
//             },
//             ExitSettings = new StrategyExitSettings
//             {
//                 TakeProfit = new Exit
//                 {
//                     CandleType = ExitCandleType.PreviousCandle,
//                     Type = ExitValueType.percent,
//                     Value = 12.5f,
//                     PriceActionType = PriceActionType.close
//                 },
//                 StopLoss = new Exit
//                 {
//                     CandleType = ExitCandleType.PreviousCandle,
//                     Type = ExitValueType.percent,
//                     Value = -10f,
//                     PriceActionType = PriceActionType.close
//                 },
//                 TimedExit = new TimedExit
//                 {
//                     Timeframe = new Timeframe(4, Timespan.hour)
//                 }
//             }
//         };
//
//         // Act
//         var response = await _classUnderTest.FunctionHandler(request, _context);
//
//         // Assert
//         response.Results.Count.Should().Be(24);
//         response.Hold.WinRatio.Should().BeApproximately(.70f, .01f);
//         response.High.WinRatio.Should().BeApproximately(.91f, 0.1f);
//     }
//
//     [SkippableFact]
//     public async Task Extended_Timed_Exit()
//     {
//         // Arrange
//         var request = new WorkerRequest
//         {
//             Date = DateTimeOffset.Parse("2025-05-29"),
//             PositionSettings = new StrategyPositionSettings
//             {
//                 AllowSimultaneous = false,
//                 MaxConcurrentPositions = 1000,
//                 Model = new PositionModel
//                 {
//                     Type = PositionType.Fixed,
//                     Size = 1000
//                 },
//                 Cooldown = new Timeframe(15, Timespan.minute),
//             },
//             EntrySettings = new StrategyEntrySettings
//             {
//                 Filters =
//                 [
//                     "close > 20 [1m]",
//                     "adv() > 1000000 [1d]",
//                     "macd(12,26,9,ema) < 1",
//                     "crosses_over(macd(12,26,9,ema), macd(12,26,9,ema).signal) [1m]"
//                 ]
//             },
//             ExitSettings = new StrategyExitSettings
//             {
//                 TakeProfit = new Exit
//                 {
//                     CandleType = ExitCandleType.PreviousCandle,
//                     Type = ExitValueType.percent,
//                     Value = 10,
//                     PriceActionType = PriceActionType.close
//                 },
//                 StopLoss = new Exit
//                 {
//                     CandleType = ExitCandleType.PreviousCandle,
//                     Type = ExitValueType.percent,
//                     Value = -10,
//                     PriceActionType = PriceActionType.close
//                 },
//                 TimedExit = new TimedExit
//                 {
//                     Timeframe = new Timeframe(2, Timespan.day)
//                 }
//             }
//         };
//
//         // Act
//         var response = await _classUnderTest.FunctionHandler(request, _context);
//
//         // Assert
//         response.Results.Count.Should().Be(860);
//
//         response.Results.First().Hold.SoldAt.Hour.Should().Be(9);
//         response.Results.First().Hold.SoldAt.Minute.Should().Be(30);
//     }
//
//     [SkippableFact]
//     public async Task Test_Different_Exits()
//     {
//         // Arrange
//         var request = new WorkerRequest
//         {
//             Date = DateTimeOffset.Parse("2025-05-29"),
//             PositionSettings = new StrategyPositionSettings
//             {
//                 AllowSimultaneous = false,
//                 MaxConcurrentPositions = 1000,
//                 Model = new PositionModel
//                 {
//                     Type = PositionType.Fixed,
//                     Size = 1000
//                 },
//                 Cooldown = new Timeframe(15, Timespan.minute),
//             },
//             EntrySettings = new StrategyEntrySettings
//             {
//                 Filters =
//                 [
//                     "close > 20 [1m]",
//                     "adv() > 1000000 [1d]",
//                     "macd(12,26,9,ema) < 1 [1m]"
//                 ]
//             },
//             ExitSettings = new StrategyExitSettings
//             {
//                 TakeProfit = new Exit
//                 {
//                     CandleType = ExitCandleType.PreviousCandle,
//                     Type = ExitValueType.percent,
//                     Value = 10,
//                     PriceActionType = PriceActionType.close
//                 },
//                 StopLoss = new Exit
//                 {
//                     CandleType = ExitCandleType.PreviousCandle,
//                     Type = ExitValueType.percent,
//                     Value = -5,
//                     PriceActionType = PriceActionType.close
//                 },
//                 TimedExit = new TimedExit
//                 {
//                     Timeframe = new Timeframe(30, Timespan.minute)
//                 }
//             }
//         };
//
//         // Act & Assert
//         var response1 = await _classUnderTest.FunctionHandler(request, _context);
//
//         response1.Results.Count.Should().Be(861);
//         var stoppedOut1 = response1.Results.Where(q => q.Hold.StoppedOut).ToList();
//         stoppedOut1.Count.Should().BeGreaterThan(0);
//
//         request.ExitSettings.StopLoss.Value = -1;
//
//         var response2 = await _classUnderTest.FunctionHandler(request, _context);
//
//         response2.Results.Count.Should().Be(861);
//         var stoppedOut2 = response2.Results.Where(q => q.Hold.StoppedOut).ToList();
//         stoppedOut2.Count.Should().BeGreaterThan(stoppedOut1.Count);
//     }
// }
