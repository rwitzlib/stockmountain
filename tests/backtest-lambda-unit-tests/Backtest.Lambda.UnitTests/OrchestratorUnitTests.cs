// using Amazon;
// using Amazon.Lambda.TestUtilities;
// using Amazon.SimpleSystemsManagement;
// using Amazon.SimpleSystemsManagement.Model;
// using Backtest.Lambda.Repository;
// using FluentAssertions;
// using MarketViewer.Contracts.Enums;
// using MarketViewer.Contracts.Enums.Backtest;
// using MarketViewer.Contracts.Models;
// using MarketViewer.Contracts.Models.Strategy;
// using MarketViewer.Contracts.Records;
// using MarketViewer.Contracts.Records.Backtest;
// using MarketViewer.Contracts.Requests.Market.Backtest;
// using Microsoft.Extensions.DependencyInjection;
// using System.Net;
// using Environment = System.Environment;
//
// namespace Backtest.Lambda.UnitTests;
//
// public class OrchestratorUnitTests
// {
//     private readonly TestLambdaContext _context;
//     private readonly OrchestratorFunction _classUnderTest;
//     private readonly IServiceProvider _serviceProvider;
//     private readonly BacktestRepository _backtestRepository;
//     private readonly UserRepository _userRepository;
//
//     public OrchestratorUnitTests()
//     {
//         Skip.If(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AWS_DEPLOYMENT_ROLE")));
//
//         Environment.SetEnvironmentVariable("MEMORY", "2048");
//
//         using var client = new AmazonSimpleSystemsManagementClient(RegionEndpoint.USEast2);
//         var response = client.GetParameterAsync(new GetParameterRequest
//         {
//             Name = "/tokens/massive",
//             WithDecryption = true
//         }).Result;
//
//         if (response.HttpStatusCode != HttpStatusCode.OK)
//         {
//             throw new Exception();
//         }
//         Environment.SetEnvironmentVariable("MASSIVE_TOKEN", response.Parameter.Value);
//
//         _context = new TestLambdaContext();
//         _classUnderTest = new OrchestratorFunction();
//         _serviceProvider = _classUnderTest.ServiceProvider;
//         _backtestRepository = _serviceProvider.GetRequiredService<BacktestRepository>();
//         _userRepository = _serviceProvider.GetRequiredService<UserRepository>();
//     }
//
//     [SkippableFact]
//     public async Task SingleFilter()
//     {
//         // Arrange
//         _userRepository.Put(new UserRecord
//         {
//             Id = "orchestrator-unittest-user",
//             Credits = 5000,
//         }).Wait();
//         _backtestRepository.Put(new BacktestContextRecord
//         {
//             Id = "orchestrator-unittest-singlefilter",
//             UserId = "orchestrator-unittest-user",
//             Status = BacktestStatus.Pending,
//         }).Wait();
//
//         var request = new OrchestratorRequest
//         {
//             Id = "orchestrator-unittest-singlefilter",
//             Start = DateTimeOffset.Parse("2025-05-27"),
//             End = DateTimeOffset.Parse("2025-05-30"),
//             PositionSettings = new StrategyPositionSettings
//             {
//                 StartingBalance = 5000,
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
//                     "close > 500 [1m]"
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
//         await _classUnderTest.FunctionHandler(request, _context);
//     }
//
//     [SkippableFact]
//     public async Task Test_Different_Exits()
//     {
//         // Arrange
//         _userRepository.Put(new UserRecord
//         {
//             Id = "orchestrator-unittest-user",
//             Credits = 5000,
//         }).Wait();
//         _backtestRepository.Put(new BacktestContextRecord
//         {
//             Id = "orchestrator-unittest-singlefilter",
//             UserId = "orchestrator-unittest-user",
//             Status = BacktestStatus.Pending,
//             Start = "2025-05-27",
//             End = "2025-05-30",
//         }).Wait();
//
//         var request = new OrchestratorRequest
//         {
//             Id = "orchestrator-unittest-singlefilter",
//             Start = DateTimeOffset.Parse("2025-05-27"),
//             End = DateTimeOffset.Parse("2025-05-30"),
//             PositionSettings = new StrategyPositionSettings
//             {
//                 StartingBalance = 5000,
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
//                     "sma(10) > sma(20) [1m]",
//                     "close < 20 [1m]",
//                     "adv() > 10000 [1m]"
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
//         await _classUnderTest.FunctionHandler(request, _context);
//
//         var record1 = await _backtestRepository.Get("orchestrator-unittest-singlefilter");
//
//         request.ExitSettings.TakeProfit.Value = 5;
//         request.ExitSettings.StopLoss.Value = -5;
//
//         _backtestRepository.Put(new BacktestContextRecord
//         {
//             Id = "orchestrator-unittest-singlefilter",
//             UserId = "orchestrator-unittest-user",
//             Status = BacktestStatus.Pending,
//             Start = "2025-05-27",
//             End = "2025-05-30",
//         }).Wait();
//
//         await _classUnderTest.FunctionHandler(request, _context);
//
//         var record2 = await _backtestRepository.Get("orchestrator-unittest-singlefilter");
//
//         record1.HoldProfit.Should().NotBe(record2.HoldProfit);
//         record1.HighProfit.Should().NotBe(record2.HighProfit);
//     }
// }
