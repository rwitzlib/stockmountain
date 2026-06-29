//using Quartz;
//using System.Net.Http.Headers;
//using MarketViewer.Contracts.Requests.Market.Scan;
//using MarketViewer.Contracts.Responses.Market;
//using MarketViewer.Contracts.Enums.Strategy;
//using MarketViewer.Contracts.Dtos;
//using Optimus.Adapters;
//using MarketViewer.Contracts.Records;
//using Optimus.Utilities;
//using Optimus.Repositories;
//using MarketViewer.Contracts.Requests.Market;
//using MarketViewer.Contracts.Enums;

//namespace Optimus.Services;

//public class TradingWorker(
//    UserRepository userRepository,
//    StrategyRepository strategyRepository,
//    TradeRepository tradeRepository,
//    HttpClient httpClient,
//    AdaptorFactory adapterFactory,
//    ILogger<TradingWorker> logger) : IJob
//{

//    private static readonly TimeSpan Offset = TimeZoneInfo.FindSystemTimeZoneById("America/New_York").IsDaylightSavingTime(DateTimeOffset.Now.Date) ? TimeSpan.FromHours(-4) : TimeSpan.FromHours(-5);
//    private readonly DateTimeOffset MarketOpen = new (DateTimeOffset.Now.Year, DateTimeOffset.Now.Month, DateTimeOffset.Now.Day, 9, 30, 0, Offset);
//    private readonly DateTimeOffset MarketClose = new (DateTimeOffset.Now.Year, DateTimeOffset.Now.Month, DateTimeOffset.Now.Day, 15, 58, 0, Offset); // Add small buffer

//    private readonly List<string> Tickers = [];

//    public async Task Execute(IJobExecutionContext context)
//    {
//        // Replace this with call to polygon to check if market is open.
//        if (DateTimeOffset.Now.DayOfWeek == DayOfWeek.Saturday || DateTimeOffset.Now.DayOfWeek == DayOfWeek.Sunday)
//        {
//            return;
//        }

//        var userId = context.MergedJobDataMap.GetString("userId");

//        var user = await userRepository.Get(userId);

//        if (user is null || user.Role < UserRole.Advanced)
//        {
//            return;
//        }

//        while (DateTimeOffset.Now >= MarketOpen && DateTimeOffset.Now <= MarketClose)
//        {
//            try
//            {
//                if (DateTimeOffset.Now.Second < 3)
//                {
//                    await Task.Delay(1000);
//                }

//                // TODO: Get user from context
//                var strategies = await strategyRepository.ListByUser("rob.witzlib@gmail.com");
//                var enabledStrategies = strategies.Where(q => q.State == StrategyStateType.Active).ToList();

//                var tasks = new List<Task>();

//                foreach (var strategy in enabledStrategies)
//                {
//                    var token = strategy.Integration switch
//                    {
//                        IntegrationType.Default => user.Tokens.TryGetValue(IntegrationType.Default, out var _token) ? _token : null,
//                        _ => null
//                    };

//                    if (string.IsNullOrEmpty(token))
//                    {
//                        logger.LogInformation("No token found for strategy: {strategyId} and integration: {integration}.", strategy.Id, strategy.Integration);
//                        continue;
//                    }

//                    tasks.Add(RunStrategy(strategy, token));
//                }

//                await Task.WhenAll(tasks);

//                await Task.Delay(1000);
//            }
//            catch (Exception e)
//            {
//                logger.LogInformation("Error: {message}", e.Message);
//                logger.LogInformation("Stacktrace: {stack}", e.StackTrace);
//                await Task.Delay(1000);
//            }
//        }
//    }

//    private async Task RunStrategy(StrategyDto strategy, string token)
//    {
//        var openPositions = await tradeRepository.ListTradesByStrategy(strategy.Id, null, TradeStatus.Open);

//        var tickers = openPositions.Select(q => q.Ticker)
//            .Where(ticker => !Tickers.Contains(ticker))
//            .ToList();
//        Tickers.AddRange(tickers);

//        var sellTasks = new List<Task>();
//        foreach (var openPosition in openPositions)
//        {
//            sellTasks.Add(SellPositionIfApplicable(strategy, openPosition));
//        }
//        await Task.WhenAll(sellTasks);

//        // Scan market for stocks fitting strategy
//        logger.LogInformation("Scanning at: {time}", DateTime.Now);

//        var request = new HttpRequestMessage
//        {
//            Method = HttpMethod.Post,
//            RequestUri = new Uri(httpClient.BaseAddress, "api/scan"),
//            Headers =
//            {
//                Authorization = new AuthenticationHeaderValue("Bearer", token.Split("Bearer ")[1])
//            },
//            Content = JsonContent.Create(new ScanRequest
//            {
//                UserId = strategy.UserId,
//                Filters = strategy.EntrySettings.Filters
//            })
//        };
//        var response = await httpClient.SendAsync(request);

//        if (!response.IsSuccessStatusCode)
//        {
//            return;
//        }

//        var scanResponse = await response.Content.ReadFromJsonAsync<ScanResponse>();
                        
//        var buyTasks = new List<Task>();
//        foreach (var item in scanResponse.Items)
//        {
//            buyTasks.Add(BuyPositionIfApplicable(strategy, item));
//        }
//        await Task.WhenAll(buyTasks);
//    }

//    public async Task SellPositionIfApplicable(StrategyDto strategy, TradeRecord trade)
//    {
//        var adapter = adapterFactory.GetAdaptor(strategy.Integration);

//        if (await ShouldSell(strategy, trade))
//        {
//            await adapter.Sell(trade);
//        }
//    }

//    public async Task<bool> ShouldSell(StrategyDto strategy, TradeRecord trade)
//    {
//        var projectedExitDate = DateUtilities.GetEndDate(DateTimeOffset.Parse(trade.OpenedAt), strategy.ExitSettings.TimedExit.Timeframe);
//        if (projectedExitDate <= DateTimeOffset.Now)
//        {
//            return true;
//        }

//        return await AreSellConditionsMet(strategy, trade);
//    }

//    public async Task BuyPositionIfApplicable(StrategyDto strategy, ScanResponse.Item item)
//    {
//        logger.LogInformation("Item returned: {ticker}", item.Ticker);

//        if (Tickers.Contains(item.Ticker))
//        {
//            logger.LogInformation("Order already exists: {ticker}", item.Ticker);
//            return;
//        }

//        var adapter = adapterFactory.GetAdaptor(strategy.Integration);

//        var isSuccess = await adapter.Buy(strategy, item);

//        if (!isSuccess)
//        {
//            logger.LogInformation("Could not buy position: {ticker}.", item.Ticker);
//            return;
//        }
//        else
//        {
//            Tickers.Add(item.Ticker);
//        }
//    }

//    private async Task<bool> AreSellConditionsMet(StrategyDto strategy, TradeRecord position)
//    {
//        var positionToClose = await HitsStopLossOrTakeProfit(strategy, position);
//        if (positionToClose is not null)
//        {
//            logger.LogInformation("Closing position for {ticker}.", position.Ticker);
//            return true;
//        }

//        // TODO: Implement exit conditions for "Other" exit strategy
//        //ExitRequest.Timestamp = DateTimeOffset.Now;
//        //var response = await _httpClient.PostAsJsonAsync("api/scan/v2", ExitRequest);

//        //if (!response.IsSuccessStatusCode)
//        //{
//        //    return result;
//        //}

//        //var scanResponse = await response.Content.ReadFromJsonAsync<ScanResponse>();

//        //var matching = positions.Where(q => scanResponse.Items.Select(q => q.Ticker).Contains(q.Ticker));

//        //result.AddRange(matching);

//        return false;
//    }

//    private async Task<TradeRecord> HitsStopLossOrTakeProfit(StrategyDto strategy, TradeRecord position)
//    {
//        try
//        {
//            var stocksResponse = await httpClient.PostAsJsonAsync<StocksRequest>("api/stocks", new StocksRequest
//            {
//                Ticker = position.Ticker,
//                Multiplier = 1,
//                Timespan = Timespan.minute,
//                From = DateTimeOffset.Now.AddDays(-1),
//                To = DateTimeOffset.Now
//            });

//            if (!stocksResponse.IsSuccessStatusCode)
//            {
//                logger.LogError("Error getting price for {ticker}.", position.Ticker);
//                return null;
//            }

//            var response = await stocksResponse.Content.ReadFromJsonAsync<StocksResponse>();

//            if (response is null || response.Results is null || !response.Results.Any() || position is null)
//            {
//                return null;
//            }

//            var currentPosition = response.Results.Last().Close * position.Shares;

//            var stopLossHit = strategy.ExitSettings.StopLoss.Type switch
//            {
//                ExitValueType.flat => currentPosition - position.EntryPosition <= strategy.ExitSettings.StopLoss.Value,
//                ExitValueType.percent => (currentPosition - position.EntryPosition) / position.EntryPosition * 100 <= strategy.ExitSettings.StopLoss.Value,
//                _ => false
//            };

//            var profitTargetHit = strategy.ExitSettings.TakeProfit.Type switch
//            {
//                ExitValueType.flat => currentPosition - position.EntryPosition >= strategy.ExitSettings.TakeProfit.Value,
//                ExitValueType.percent => (currentPosition - position.EntryPosition) / position.EntryPosition * 100 >= strategy.ExitSettings.TakeProfit.Value,
//                _ => false
//            };

//            if (stopLossHit)
//            {
//                logger.LogInformation("Stop Loss hit for {ticker}.", position.Ticker);

//                position.ClosePrice = response.Results.Last().Close;
//                position.ClosePosition = currentPosition;
//                position.Profit = position.ClosePosition = position.EntryPosition;

//                return position;
//            }
//            else if (profitTargetHit)
//            {
//                logger.LogInformation("Stop Loss hit for {ticker}.", position.Ticker);

//                position.ClosePrice = response.Results.Last().Close;
//                position.ClosePosition = currentPosition;
//                position.Profit = position.ClosePosition = position.EntryPosition;

//                return position;
//            }
//            else
//            {
//                return null;
//            }
//        }
//        catch (Exception e)
//        {
//            logger.LogError("Error getting price for {ticker}.", position.Ticker);
//            logger.LogError("Error: {message}", e.Message);
//            return null;
//        }
//    }
//}