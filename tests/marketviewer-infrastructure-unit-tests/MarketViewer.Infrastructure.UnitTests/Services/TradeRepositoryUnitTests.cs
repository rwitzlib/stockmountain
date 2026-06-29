using FluentAssertions;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Records;
using MarketViewer.Contracts.Requests.Market;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Infrastructure.Config;
using MarketViewer.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Polygon.Client;
using Polygon.Client.Requests;
using Polygon.Client.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace MarketViewer.Infrastructure.UnitTests.Services;

public class TradeRepositoryUnitTests
{
    private JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private PolygonClient _polygonClient;

    public TradeRepositoryUnitTests()
    {
        _polygonClient = new PolygonClient("ok6cMrmJ9hQpw4rUzielfLc0YjHskzRS");
    }

    private class TradeData
    {
        public string Ticker { get; set; }
        public PolygonAggregateResponse Response { get; set; }
        public float AverageVolume { get; set; }
        public long? Float { get; set; }
    }

    //[Fact]
    public async Task asdf()
    {
        var json = await File.ReadAllTextAsync("./Data/trades.json");

        var trades = JsonSerializer.Deserialize<List<TradeRecord>>(json, Options);

        var tickers = trades.Select(q => q.Ticker).Distinct().ToList();

        List<Task<TradeData>> tasks = new();
        foreach (var ticker in tickers)
        {
            tasks.Add(GetData(ticker));
        }
        var data = await Task.WhenAll(tasks);

        int floatCondition = 23000000;
        int dailyVolume = 10000000;
        float priceCondition = 15;
        int hour = 12;

        var floatTickers = data.Where(q => q.Float > floatCondition).Select(q => q.Ticker).ToList();
        var volumeTickers = data.Where(q => q.AverageVolume > dailyVolume).Select(q => q.Ticker).ToList();

        var floatTrades = trades.Where(q => floatTickers.Contains(q.Ticker)).ToList();
        var volumeTrades = trades.Where(q => volumeTickers.Contains(q.Ticker)).ToList();
        var priceTrades = trades.Where(q => q.EntryPrice > priceCondition).ToList();
        var volumePriceTrades = volumeTrades.Where(q => q.EntryPrice > priceCondition).ToList();
        var tradesThatExitNextDay = trades.Where(q =>
        {
            if (string.IsNullOrEmpty(q.OpenedAt) || string.IsNullOrEmpty(q.ClosedAt))
                return false;
            var openedAt = DateTimeOffset.Parse(q.OpenedAt);
            var closedAt = DateTimeOffset.Parse(q.ClosedAt);
            return (closedAt.Date > openedAt.Date) && (closedAt - openedAt).TotalDays <= 2;
        }).ToList();
        var hourTrades = trades.Where(q => DateTimeOffset.Parse(q.OpenedAt).Hour >= hour).ToList();

        var profit = trades.Sum(q => q.Profit);
        var floatProfit = floatTrades.Sum(q => q.Profit);
        var volumeProfit = volumeTrades.Sum(q => q.Profit);
        var priceProfit = priceTrades.Sum(q => q.Profit);
        var volumePriceProfit = volumePriceTrades.Sum(q => q.Profit);
        var nextDayExitProfit = tradesThatExitNextDay.Sum(q => q.Profit);
        var hourProfit = hourTrades.Sum(q => q.Profit);

        var profitPerTrade = profit / trades.Count;
        var floatProfitPerTrade = floatProfit / floatTrades.Count;
        var volumeProfitPerTrade = volumeProfit / volumeTrades.Count;
        var priceProfitPerTrade = priceProfit / priceTrades.Count;
        var volumePriceProfitPerTrade = volumePriceProfit / volumePriceTrades.Count;
        var nextDayExitProfitPerTrade = nextDayExitProfit / tradesThatExitNextDay.Count;
        var hourProfitPerTrade = hourProfit / hourTrades.Count;

        var profitRatio = (float)trades.Where(q => q.Profit > 0).Count() / trades.Count;
        var floatProfitRatio = (float)floatTrades.Where(q => q.Profit > 0).Count() / floatTrades.Count;
        var volumeProfitRatio = (float)volumeTrades.Where(q => q.Profit > 0).Count() / volumeTrades.Count;
        var priceProfitRatio = (float)priceTrades.Where(q => q.Profit > 0).Count() / priceTrades.Count;
        var volumePriceProfitRatio = (float)volumePriceTrades.Where(q => q.Profit > 0).Count() / volumePriceTrades.Count;
        var nextDayExitProfitRatio = (float)tradesThatExitNextDay.Where(q => q.Profit > 0).Count() / tradesThatExitNextDay.Count;
        var hourProfitRatio = (float)hourTrades.Where(q => q.Profit > 0).Count() / hourTrades.Count;

        var concurrentTrades = GetConcurrentTrades(trades);
        var concurrentFloatTrades = GetConcurrentTrades(floatTrades);
        var concurrentVolumeTrades = GetConcurrentTrades(volumeTrades);
        var concurrentPriceTrades = GetConcurrentTrades(priceTrades);
        var concurrentVolumePriceTrades = GetConcurrentTrades(volumePriceTrades);
        var concurrentNextDayExitTrades = GetConcurrentTrades(tradesThatExitNextDay);
        var concurrentHourTrades = GetConcurrentTrades(hourTrades);

        // we want to find why the strategy exits the next day and what time it usually exits
        // is it still profitable if it exits at market open the next day?
        var avgExitTime = tradesThatExitNextDay
            .Select(q => DateTimeOffset.Parse(q.ClosedAt).TimeOfDay.TotalHours)
            .Average();

        // average exit time is near 9:40.  It should have been closer to 9:30, but dynamodb was throttling, so this pushed the exit times later
        // we want to figure out if the throttling is a good or bad thing.  So lets get profit of trades in different timeframes
        var exitBy935 = tradesThatExitNextDay.Where(q => DateTimeOffset.Parse(q.ClosedAt).TimeOfDay <= TimeSpan.FromHours(9.5 + 5.0 / 60)).ToList();
        var exitBy940 = tradesThatExitNextDay.Where(q => DateTimeOffset.Parse(q.ClosedAt).TimeOfDay <= TimeSpan.FromHours(9.5 + 10.0 / 60)).ToList();
        var exitBy945 = tradesThatExitNextDay.Where(q => DateTimeOffset.Parse(q.ClosedAt).TimeOfDay <= TimeSpan.FromHours(9.5 + 15.0 / 60)).ToList();
        var exitBy950 = tradesThatExitNextDay.Where(q => DateTimeOffset.Parse(q.ClosedAt).TimeOfDay <= TimeSpan.FromHours(9.5 + 20.0 / 60)).ToList();
        var exitBy955 = tradesThatExitNextDay.Where(q => DateTimeOffset.Parse(q.ClosedAt).TimeOfDay <= TimeSpan.FromHours(9.5 + 25.0 / 60)).ToList();
        var exitBy1000 = tradesThatExitNextDay.Where(q => DateTimeOffset.Parse(q.ClosedAt).TimeOfDay <= TimeSpan.FromHours(10)).ToList();
        var exitBy1200 = tradesThatExitNextDay.Where(q => DateTimeOffset.Parse(q.ClosedAt).TimeOfDay <= TimeSpan.FromHours(12)).ToList();

        var exitBy935Profit = exitBy935.Sum(q => q.Profit);
        var exitBy940Profit = exitBy940.Sum(q => q.Profit);
        var exitBy945Profit = exitBy945.Sum(q => q.Profit);
        var exitBy950Profit = exitBy950.Sum(q => q.Profit);
        var exitBy955Profit = exitBy955.Sum(q => q.Profit);
        var exitBy1000Profit = exitBy1000.Sum(q => q.Profit);
        var exitBy1200Profit = exitBy1200.Sum(q => q.Profit);

        // 30% of trades exited between 9:35 and 10 accounted for 50% of the profit up until that point
        // 65% of profit came from last 100 trades between 10 and 12
        // There may be a benefit to waiting later to exit
        var adjustedExitTasks = new List<Task<TradeRecord>>();
        foreach (var trade in tradesThatExitNextDay)
        {
            adjustedExitTasks.Add(AdjustExit(trade, 7));
        }
        var timeData = (await Task.WhenAll(adjustedExitTasks)).Where(q => q is not null);

        var adjustedProfit = timeData.Sum(q => q.Profit);
        var adjustedProfitPerTrade = adjustedProfit / timeData.Count();
        var adjustedProfitRatio = (float)timeData.Where(q => q.Profit > 0).Count() / timeData.Count();
        var adjustedConcurrentTrades = GetConcurrentTrades(timeData.ToList());
    }

    private async Task<TradeData> GetData(string ticker)
    {
        var response = await _polygonClient.GetAggregates(new PolygonAggregateRequest
        {
            Ticker = ticker,
            Multiplier = 1,
            Timespan = Timespan.day.ToString(),
            From = DateTimeOffset.Parse("2025-01-01").ToString("yyyy-MM-dd"),
            To = DateTimeOffset.Parse("2025-09-17").ToString("yyyy-MM-dd")
        });
        var tickerDetails = await _polygonClient.GetTickerDetails(ticker);

        return new TradeData
        {
            Ticker = ticker,
            Response = response,
            AverageVolume = response.Results.Average(q => q.Volume),
            Float = tickerDetails.TickerDetails?.Float
        };
    }
    
    private async Task<TradeRecord> AdjustExit(TradeRecord trade, int daysLater = 0)
    {
        try
        {
            var openedAt = DateTimeOffset.Parse(trade.OpenedAt);
            var closedAt = DateTimeOffset.Parse(trade.ClosedAt);
            var adjustedExit = new DateTimeOffset(closedAt.Year, closedAt.Month, closedAt.Day, 12, 0, 0, closedAt.Offset).AddDays(daysLater);
            var response = await _polygonClient.GetAggregates(new PolygonAggregateRequest
            {
                Ticker = trade.Ticker,
                Multiplier = 1,
                Timespan = Timespan.minute.ToString(),
                From = openedAt.ToString("yyyy-MM-dd"),
                To = adjustedExit.ToString("yyyy-MM-dd")
            });

            var exitCandle = response.Results.Where(q => DateTimeOffset.FromUnixTimeMilliseconds(q.Timestamp).ToOffset(closedAt.Offset) <= adjustedExit).OrderBy(q => q.Timestamp).Last();
            var exitPrice = exitCandle.Close;
            var tradeExitPrice = trade.ClosePrice;
            var exitDateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(exitCandle.Timestamp).ToOffset(adjustedExit.Offset);
            var record = new TradeRecord
            {
                Ticker = trade.Ticker,
                OpenedAt = trade.OpenedAt,
                ClosedAt = adjustedExit.ToString(),
                EntryPrice = trade.EntryPrice,
                ClosePrice = exitCandle.Close
            };
            
            var profit = (record.ClosePrice - record.EntryPrice) * trade.Shares;

            if (profit <= -75)
            {
                //record.Profit = -75;
            }
            //else if (profit >= 150)
            //{
            //    record.Profit = 150;
            //}
            else
            {
                record.Profit = profit;
            }
            return record;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    private static int GetConcurrentTrades(List<TradeRecord> trades)
    {
        if (trades == null || trades.Count == 0)
            return 0;

        // Create events list: (timestamp, isOpening)
        var events = new List<(DateTimeOffset timestamp, bool isOpening)>();

        foreach (var trade in trades)
        {
            if (!string.IsNullOrEmpty(trade.OpenedAt))
            {
                events.Add((DateTimeOffset.Parse(trade.OpenedAt), true));
            }

            if (!string.IsNullOrEmpty(trade.ClosedAt))
            {
                events.Add((DateTimeOffset.Parse(trade.ClosedAt), false));
            }
        }

        // Sort events by timestamp, openings before closings at the same time
        events.Sort((a, b) =>
        {
            var timeCompare = a.timestamp.CompareTo(b.timestamp);
            if (timeCompare != 0)
                return timeCompare;

            // If same timestamp, openings come before closings
            if (a.isOpening && !b.isOpening)
                return -1;
            if (!a.isOpening && b.isOpening)
                return 1;

            return 0;
        });

        int currentConcurrent = 0;
        int maxConcurrent = 0;

        foreach (var (timestamp, isOpening) in events)
        {
            if (isOpening)
            {
                currentConcurrent++;
                maxConcurrent = Math.Max(maxConcurrent, currentConcurrent);
            }
            else
            {
                currentConcurrent--;
            }
        }

        return maxConcurrent;
    }
}
