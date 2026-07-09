using Amazon.Lambda;
using Amazon.Lambda.Model;
using MarketViewer.Contracts.Enums.Backtest;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Models.Backtest;
using MarketViewer.Contracts.Requests.Market.Backtest;
using MarketViewer.Contracts.Responses.Market.Backtest;
using System.Text.Json;
using MarketViewer.Core.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading.Tasks;
using MarketViewer.Infrastructure.Config;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using MarketViewer.Contracts.Models.Strategy;
using MarketViewer.Contracts.Records.Backtest;
using MarketViewer.Core.Auth;

namespace MarketViewer.Application.Handlers.Market.Backtest;

public class BacktestHandler(
    BacktestConfig config,
    AuthContext authContext,
    IAmazonLambda lambda,
    IBacktestRepository repository,
    ILogger<BacktestHandler> logger)
{
    private readonly TimeZoneInfo _timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    private readonly JsonSerializerOptions _options = new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<OperationResult<BacktestEntryResponse>> Create(BacktestCreateRequest request)
    {
        try
        {
            logger.LogInformation("Creating backtest with ID: {id} for user {userId}", request.Id, authContext.UserId);
            
            var record = new BacktestContextRecord
            {
                Id = request.Id,
                UserId = authContext.UserId,
                Status = BacktestStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Start = request.Start.Date.ToString("yyyy-MM-dd"),
                End = request.End.Date.ToString("yyyy-MM-dd"),
                Request = request
            };

            await repository.Put(record);

            var json = JsonSerializer.Serialize(request, _options);

            // Change this to SQS eventually for better scalability
            _ = lambda.InvokeAsync(new InvokeRequest
            {
                FunctionName = config.LambdaName,
                Payload = json
            });

            return new OperationResult<BacktestEntryResponse>
            {
                Status = HttpStatusCode.OK,
                Data = new BacktestEntryResponse
                {
                    Id = request.Id,
                    Status = BacktestStatus.Pending,
                    CreatedAt = DateTimeOffset.Parse(record.CreatedAt),
                }
            };
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating backtest with ID: {id}", request.Id);
            return new OperationResult<BacktestEntryResponse>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["Internal error."]
            };
        }
    }

    public async Task<OperationResult<List<BacktestEntryResponse>>> List(string userId)
    {
        try
        {
            var records = await repository.List(userId);

            List<BacktestEntryResponse> entries = [];
            foreach (var record in records)
            {
                var entry = new BacktestEntryResponse
                {
                    Id = record.Id,
                    Status = record.Status,
                    CreatedAt = DateTimeOffset.Parse(record.CreatedAt),
                    CreditsUsed = record.CreditsUsed,
                    HoldProfit = record.HoldProfit,
                    HighProfit = record.HighProfit,
                    ConditionalProfit = record.ConditionalProfit,
                    Request = record.Request,
                    Start = DateTimeOffset.Parse(record.Start),
                    End = DateTimeOffset.Parse(record.End),
                    DurationSeconds = record.DurationSeconds,
                    Errors = record.Errors
                };

                // Deserialize stats JSON if available (only for completed backtests)
                if (record.Status == BacktestStatus.Completed)
                {
                    if (!string.IsNullOrEmpty(record.HoldStatsJson))
                    {
                        try
                        {
                            entry.HoldStats = JsonSerializer.Deserialize<BacktestEntryStatsSummary>(record.HoldStatsJson);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to deserialize HoldStatsJson for backtest {id}", record.Id);
                        }
                    }

                    if (!string.IsNullOrEmpty(record.HighStatsJson))
                    {
                        try
                        {
                            entry.HighStats = JsonSerializer.Deserialize<BacktestEntryStatsSummary>(record.HighStatsJson);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to deserialize HighStatsJson for backtest {id}", record.Id);
                        }
                    }
                }

                entries.Add(entry);
            }

            return new OperationResult<List<BacktestEntryResponse>>
            {
                Status = HttpStatusCode.OK,
                Data = entries
            };
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error listing backtests for user: {userId}", userId);
            return new OperationResult<List<BacktestEntryResponse>>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["Internal error."]
            };
        }
    }

    public async Task<OperationResult<BacktestEntryResponse>> GetEntry(string id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return new OperationResult<BacktestEntryResponse>
                {
                    Status = HttpStatusCode.BadRequest,
                    ErrorMessages = ["Invalid backtest ID."]
                };
            }

            logger.LogInformation("Getting backtest entry for ID: {id}", id);

            var record = await repository.Get(id);

            if (record is null)
            {
                return new OperationResult<BacktestEntryResponse>
                {
                    Status = HttpStatusCode.NotFound,
                    ErrorMessages = ["Backtest not found."]
                };
            }

            var entry = new BacktestEntryResponse
            {
                Id = record.Id,
                Status = record.Status,
                CreatedAt = DateTimeOffset.Parse(record.CreatedAt),
                CreditsUsed = record.CreditsUsed,
                HoldProfit = record.HoldProfit,
                HighProfit = record.HighProfit,
                ConditionalProfit = record.ConditionalProfit,
                Request = record.Request,
                Start = DateTimeOffset.Parse(record.Start),
                End = DateTimeOffset.Parse(record.End),
                DurationSeconds = record.DurationSeconds,
                Errors = record.Errors
            };

            // Deserialize stats JSON if available (only for completed backtests)
            if (record.Status == BacktestStatus.Completed)
            {
                if (!string.IsNullOrEmpty(record.HoldStatsJson))
                {
                    try
                    {
                        entry.HoldStats = JsonSerializer.Deserialize<BacktestEntryStatsSummary>(record.HoldStatsJson);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to deserialize HoldStatsJson for backtest {id}", record.Id);
                    }
                }

                if (!string.IsNullOrEmpty(record.HighStatsJson))
                {
                    try
                    {
                        entry.HighStats = JsonSerializer.Deserialize<BacktestEntryStatsSummary>(record.HighStatsJson);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to deserialize HighStatsJson for backtest {id}", record.Id);
                    }
                }
            }

            return new OperationResult<BacktestEntryResponse>
            {
                Status = HttpStatusCode.OK,
                Data = entry
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting backtest entry for ID: {id}", id);
            return new OperationResult<BacktestEntryResponse>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["Internal error."]
            };
        }
    }

    public async Task<OperationResult<BacktestResultResponse>> GetResult(string id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return new OperationResult<BacktestResultResponse>
                {
                    Status = HttpStatusCode.BadRequest,
                    ErrorMessages = ["Invalid backtest ID."]
                };
            }

            logger.LogInformation("Getting backtest result for ID: {id}", id);

            var record = await repository.Get(id);

            if (record is null)
            {
                return new OperationResult<BacktestResultResponse>
                {
                    Status = HttpStatusCode.NotFound,
                    ErrorMessages = ["Backtest not found."]
                };
            }

            if (record.Status != BacktestStatus.Completed)
            {
                return new OperationResult<BacktestResultResponse>
                {
                    Status = HttpStatusCode.BadRequest,
                    ErrorMessages = ["Backtest is not completed yet."]
                };
            }

            var entries = await repository.GetBacktestResultsFromS3(record);

            if (entries is null || !entries.Any())
            {
                return new OperationResult<BacktestResultResponse>
                {
                    Status = HttpStatusCode.NotFound,
                    ErrorMessages = ["No results found for this backtest."]
                };
            }

            var availableFundsHold = record.Request.PositionSettings.StartingBalance;
            //var availableFundsOther = record.Request.PositionSettings.StartingBalance;
            var availableFundsHigh = record.Request.PositionSettings.StartingBalance;

            var holdOpenPositions = new List<BacktestEntryResultCollection>();
            //var otherOpenPositions = new List<BacktestEntryResultCollection>();
            var highOpenPositions = new List<BacktestEntryResultCollection>();

            int maxConcurrentHoldPositions = 0;
            int maxConcurrentHighPositions = 0;
            //int maxConcurrentOtherPositions = 0;
            int totalHoldTrades = 0;
            int totalHighTrades = 0;

            var dayRange = GetDateRange(record, entries);

            var backtestDayResults = new List<BacktestDayResultV3>();

            foreach (var day in dayRange)
            {
                var offset = _timeZone.IsDaylightSavingTime(day.Date) ? TimeSpan.FromHours(-4) : TimeSpan.FromHours(-5);
                var marketOpen = new DateTimeOffset(day.Year, day.Month, day.Day, 9, 30, 0, offset);
                var marketClose = new DateTimeOffset(day.Year, day.Month, day.Day, 16, 0, 0, offset);

                var entry = entries.FirstOrDefault(q => q.Date.ToString("yyyy-MM-dd") == day.ToString("yyyy-MM-dd"));

                var backtestEntryDay = new BacktestDayResultV3
                {
                    Date = day,
                    Hold = new BacktestDayDetails
                    {
                        StartCashAvailable = availableFundsHold,
                        OpenPositions = holdOpenPositions.Count,
                        MaxConcurrentPositions = holdOpenPositions.Count,
                        Bought = [],
                        Sold = []
                    },
                    High = new BacktestDayDetails
                    {
                        StartCashAvailable = availableFundsHigh,
                        OpenPositions = highOpenPositions.Count,
                        MaxConcurrentPositions = highOpenPositions.Count,
                        Bought = [],
                        Sold = []
                    },
                    //Conditional = parameters.ExitInfo.ConditionalExit is null ? null : new BacktestDayDetails
                    //{
                    //    StartCashAvailable = availableFundsOther,
                    //    Bought = [],
                    //    Sold = []
                    //}
                };

                var holdDailyMax = holdOpenPositions.Count;
                var highDailyMax = highOpenPositions.Count;

                for (int i = 0; i < (marketClose - marketOpen).TotalMinutes; i++)
                {
                    var currentTime = marketOpen.AddMinutes(i);

                    SellPositionIfApplicable("hold", holdOpenPositions, currentTime, ref availableFundsHold, backtestEntryDay);
                    //if (parameters.ExitInfo.ConditionalExit is not null)
                    //{
                    //    SellPositionIfApplicable("other", otherOpenPositions, currentTime, ref availableFundsOther, backtestEntryDay);
                    //}
                    SellPositionIfApplicable("high", highOpenPositions, currentTime, ref availableFundsHigh, backtestEntryDay);

                    BuyPositionIfApplicable(
                        "hold",
                        entry,
                        currentTime,
                        record.Request.PositionSettings,
                        ref availableFundsHold,
                        holdOpenPositions,
                        backtestEntryDay);
                    //if (parameters.ExitInfo.ConditionalExit is not null)
                    //{
                    //    BuyPositionIfApplicable("other", entry, currentTime, parameters, ref availableFundsOther, otherOpenPositions, backtestEntryDay);
                    //}
                    BuyPositionIfApplicable(
                        "high",
                        entry,
                        currentTime,
                        record.Request.PositionSettings,
                        ref availableFundsHigh,
                        highOpenPositions,
                        backtestEntryDay);

                    var currentHoldOpen = holdOpenPositions.Count;
                    var currentHighOpen = highOpenPositions.Count;

                    holdDailyMax = Math.Max(holdDailyMax, currentHoldOpen);
                    highDailyMax = Math.Max(highDailyMax, currentHighOpen);

                    backtestEntryDay.Hold.OpenPositions = currentHoldOpen;
                    backtestEntryDay.High.OpenPositions = currentHighOpen;

                    maxConcurrentHoldPositions = Math.Max(maxConcurrentHoldPositions, currentHoldOpen);
                    maxConcurrentHighPositions = Math.Max(maxConcurrentHighPositions, currentHighOpen);
                    //if (parameters.ExitInfo.ConditionalExit is not null)
                    //{
                    //    maxConcurrentOtherPositions = otherOpenPositions.Count > maxConcurrentHighPositions ? otherOpenPositions.Count : maxConcurrentHighPositions;
                    //}
                }

                backtestEntryDay.Hold.OpenPositions = holdOpenPositions.Count;
                backtestEntryDay.Hold.EndCashAvailable = availableFundsHold;
                backtestEntryDay.Hold.TotalBalance = holdOpenPositions.Sum(q => q.StartPosition) + backtestEntryDay.Hold.EndCashAvailable;
                backtestEntryDay.Hold.MaxConcurrentPositions = holdDailyMax;

                //if (parameters.ExitInfo.ConditionalExit is not null)
                //{
                //    backtestEntryDay.Conditional.EndCashAvailable = availableFundsOther;
                //    backtestEntryDay.Conditional.TotalBalance = otherOpenPositions.Sum(q => q.StartPosition) + backtestEntryDay.Conditional.EndCashAvailable;
                //}

                backtestEntryDay.High.EndCashAvailable = availableFundsHigh;
                backtestEntryDay.High.TotalBalance = highOpenPositions.Sum(q => q.StartPosition) + backtestEntryDay.High.EndCashAvailable;
                backtestEntryDay.High.MaxConcurrentPositions = highDailyMax;
                backtestEntryDay.High.OpenPositions = highOpenPositions.Count;

                totalHoldTrades += backtestEntryDay.Hold.TradesTaken;
                totalHighTrades += backtestEntryDay.High.TradesTaken;

                backtestDayResults.Add(backtestEntryDay);
            }

            var holdWins = backtestDayResults.SelectMany(q => q.Hold.Sold).Where(q => q.Profit > 0);
            var holdLosses = backtestDayResults.SelectMany(q => q.Hold.Sold).Where(q => q.Profit < 0);

            //var otherWins = parameters.ExitInfo.ConditionalExit is null ? [] : backtestDayResults.SelectMany(q => q.Conditional.Sold).Where(q => q.Profit > 0);
            //var otherLosses = parameters.ExitInfo.ConditionalExit is null ? [] : backtestDayResults.SelectMany(q => q.Conditional.Sold).Where(q => q.Profit < 0);

            var highWins = backtestDayResults.SelectMany(q => q.High.Sold).Where(q => q.Profit > 0);
            var highLosses = backtestDayResults.SelectMany(q => q.High.Sold).Where(q => q.Profit < 0);

            var holdPerformance = CalculatePerformanceMetrics(
                backtestDayResults,
                result => result.Hold,
                record.Request.PositionSettings.StartingBalance);

            var highPerformance = CalculatePerformanceMetrics(
                backtestDayResults,
                result => result.High,
                record.Request.PositionSettings.StartingBalance);

            var dict = new Dictionary<DateTimeOffset, List<DateTimeOffset>>();
            foreach (var entry in entries)
            {
                dict[entry.Date] = entry.Results.Select(q => q.High.SoldAt).ToList();
            }
            List<int> list = [];
            foreach (var kvp in dict)
            {
                foreach (var soldAt in kvp.Value)
                {
                    list.Add((soldAt - kvp.Key).Days);
                }
            }

            var avg = list.Average();

            return new OperationResult<BacktestResultResponse>
            {
                Status = HttpStatusCode.OK,
                Data = new BacktestResultResponse
                {
                    Id = record.Id,
                    CreditsUsed = record.CreditsUsed,
                    Hold = new BacktestEntryStats
                    {
                        EndBalance = availableFundsHold,
                        BalanceChange = availableFundsHold - record.Request.PositionSettings.StartingBalance,
                        WinRatio = holdWins.Any() ? (float)holdWins.Count() / (float)(holdWins.Count() + holdLosses.Count()) : 0,
                        AvgWin = holdWins.Any() ? holdWins.Average(q => q.Profit) : 0,
                        AvgLoss = holdLosses.Any() ? holdLosses.Average(q => q.Profit) : 0,
                        MaxConcurrentPositions = maxConcurrentHoldPositions,
                        TotalTradesTaken = totalHoldTrades,
                        AverageDailyReturn = holdPerformance.AverageDailyReturn,
                        DailyReturnStdDev = holdPerformance.DailyReturnStdDev,
                        SharpeRatio = holdPerformance.SharpeRatio,
                        MaxDrawdown = holdPerformance.MaxDrawdown,
                        ProfitFactor = holdPerformance.ProfitFactor
                    },
                    High = new BacktestEntryStats
                    {
                        EndBalance = availableFundsHigh,
                        BalanceChange = availableFundsHigh - record.Request.PositionSettings.StartingBalance,
                        WinRatio = highWins.Any() ? (float)highWins.Count() / (float)(highWins.Count() + highLosses.Count()) : 0,
                        AvgWin = highWins.Any() ? highWins.Average(q => q.Profit) : 0,
                        AvgLoss = highLosses.Any() ? highLosses.Average(q => q.Profit) : 0,
                        MaxConcurrentPositions = maxConcurrentHighPositions,
                        TotalTradesTaken = totalHighTrades,
                        AverageDailyReturn = highPerformance.AverageDailyReturn,
                        DailyReturnStdDev = highPerformance.DailyReturnStdDev,
                        SharpeRatio = highPerformance.SharpeRatio,
                        MaxDrawdown = highPerformance.MaxDrawdown,
                        ProfitFactor = highPerformance.ProfitFactor
                    },
                    //Other = parameters.ExitInfo.ConditionalExit is null ? null : new BacktestEntryStats
                    //{
                    //    EndBalance = availableFundsOther,
                    //    SumProfit = availableFundsOther - parameters.PositionInfo.StartingBalance,
                    //    WinRatio = otherWins.Any() ? (float)otherWins.Count() / (float)(otherWins.Count() + otherLosses.Count()) : 0,
                    //    AvgWin = otherWins.Any() ? otherWins.Average(q => q.Profit) : 0,
                    //    AvgLoss = otherLosses.Any() ? otherLosses.Average(q => q.Profit) : 0,
                    //    MaxConcurrentPositions = backtestDayResults.Any() ? backtestDayResults.Max(result => result.Conditional.OpenPositions) : 0
                    //},
                    Results = backtestDayResults,
                    Entries = entries
                }
            };

        }
        catch (Exception e)
        {
            logger.LogError(e, "Error getting backtest result for ID: {id}", id);
            return new OperationResult<BacktestResultResponse>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["Internal error."]
            };
        }
    }

    #region Private Methods

    private static IEnumerable<DateTimeOffset> GetDateRange(BacktestContextRecord record, IEnumerable<WorkerResponse> entries)
    {
        var entriesWithDates = entries.Where(q => q.Results.Any());

        if (!entriesWithDates.Any())
        {
            return [];
        }

        var lastDates = new List<DateTimeOffset>
        {
            entriesWithDates.Max(q => q.Results.Max(result => result.Hold.SoldAt)),
            entriesWithDates.Max(q => q.Results.Max(result => result.High.SoldAt)),
        };

        var otherDates = entriesWithDates.Where(q => q.Other is not null);
        if (otherDates.Any())
        {
            lastDates.Add(otherDates.Max(q => q.Results.Max(result => result.Other.SoldAt)));
        }

        if (!lastDates.Any())
        {
            return [];
        }

        var maxDate = lastDates.Max();
        var startDate = DateTimeOffset.Parse(record.Start);

        return Enumerable.Range(0, (maxDate - startDate).Days + 1)
            .Select(day => startDate.AddDays(day))
            .Where(day => day.DayOfWeek != DayOfWeek.Sunday && day.DayOfWeek != DayOfWeek.Saturday);
    }

    private static void SellPositionIfApplicable(
        string type,
        List<BacktestEntryResultCollection> openPositions,
        DateTimeOffset timestamp,
        ref float availableFunds,
        BacktestDayResultV3 backtestDay)
    {
        List<BacktestEntryResultCollection> positionsToRemove = [];

        var positionsToSell = type.ToLowerInvariant() switch
        {
            "hold" => openPositions.Where(position => position.Hold.SoldAt == timestamp),
            "high" => openPositions.Where(position => position.High.SoldAt == timestamp),
            "other" => openPositions.Where(position => position.Other.SoldAt == timestamp),
            _ => throw new NotImplementedException()
        };

        foreach (var position in positionsToSell)
        {
            availableFunds += type.ToLowerInvariant() switch
            {
                "hold" => position.Hold.EndPosition,
                "high" => position.High.EndPosition,
                "other" => position.Other.EndPosition,
                _ => throw new NotImplementedException()
            };
            positionsToRemove.Add(position);
        }

        foreach (var position in positionsToRemove)
        {
            switch (type.ToLowerInvariant())
            {
                case "hold":
                    backtestDay.Hold.Sold.Add(new BacktestDayPosition
                    {
                        Ticker = position.Ticker,
                        Price = position.Hold.EndPrice,
                        Shares = position.Shares,
                        Position = position.Hold.EndPosition,
                        Profit = position.Hold.Profit,
                        Timestamp = position.Hold.SoldAt,
                        StoppedOut = position.Hold.StoppedOut
                    });
                    backtestDay.Hold.Profit += position.Hold.Profit;
                    break;

                case "high":
                    backtestDay.High.Sold.Add(new BacktestDayPosition
                    {
                        Ticker = position.Ticker,
                        Price = position.High.EndPrice,
                        Shares = position.Shares,
                        Position = position.High.EndPosition,
                        Profit = position.High.Profit,
                        Timestamp = position.High.SoldAt,
                        StoppedOut = position.High.StoppedOut
                    });
                    backtestDay.High.Profit += position.High.Profit;
                    break;

                case "other":
                    backtestDay.Conditional.Sold.Add(new BacktestDayPosition
                    {
                        Ticker = position.Ticker,
                        Price = position.Other.EndPrice,
                        Shares = position.Shares,
                        Position = position.Other.EndPosition,
                        Profit = position.Other.Profit,
                        Timestamp = position.Other.SoldAt,
                        StoppedOut = position.Other.StoppedOut
                    });
                    backtestDay.Conditional.Profit += position.Other.Profit;
                    break;

                default: throw new NotImplementedException();
            };

            openPositions.Remove(position);
        }
    }

    private static void BuyPositionIfApplicable(
        string type,
        WorkerResponse entry,
        DateTimeOffset timestamp,
        StrategyPositionSettings positionSettings,
        ref float availableFunds,
        List<BacktestEntryResultCollection> openPositions,
        BacktestDayResultV3 backtestDay)
    {
        if (entry is null)
        {
            return;
        }

        var results = entry.Results.Where(result => result.BoughtAt == timestamp);

        foreach (var result in results)
        {
            if (availableFunds < positionSettings.Model.Size)
            {
                continue;
            }

            if (positionSettings.MaxConcurrentPositions > 0)
            {
                if (openPositions.Count >= positionSettings.MaxConcurrentPositions)
                {
                    continue;
                }
            }

            var backtestDayPosition = new BacktestDayPosition
            {
                Ticker = result.Ticker,
                Price = result.StartPrice,
                Shares = result.Shares,
                Position = result.StartPosition,
                Timestamp = result.BoughtAt,
            };

            switch (type.ToLowerInvariant())
            {
                case "hold":
                    backtestDay.Hold.Bought.Add(backtestDayPosition);
                    backtestDay.Hold.TradesTaken++;
                    break;
                case "high":
                    backtestDay.High.Bought.Add(backtestDayPosition);
                    backtestDay.High.TradesTaken++;
                    break;
                case "other":
                    backtestDay.Conditional.Bought.Add(backtestDayPosition);
                    backtestDay.Conditional.TradesTaken++;
                    break;
                default:
                    throw new NotImplementedException();
            }

            openPositions.Add(result);
            availableFunds -= result.StartPosition;
        }
    }

    private static PerformanceMetrics CalculatePerformanceMetrics(
        IEnumerable<BacktestDayResultV3> dayResults,
        Func<BacktestDayResultV3, BacktestDayDetails> selector,
        float startingBalance)
    {
        if (dayResults is null)
        {
            return PerformanceMetrics.Empty;
        }

        var details = dayResults
            .Select(selector)
            .Where(detail => detail is not null)
            .ToList();

        if (!details.Any())
        {
            return PerformanceMetrics.Empty;
        }

        var balances = new List<float> { startingBalance };
        balances.AddRange(details.Select(detail =>
            detail.TotalBalance != 0
                ? detail.TotalBalance
                : detail.EndCashAvailable != 0
                    ? detail.EndCashAvailable
                    : detail.StartCashAvailable));

        var returns = new List<double>();
        for (int i = 1; i < balances.Count; i++)
        {
            var previous = balances[i - 1];
            var current = balances[i];

            if (previous == 0)
            {
                continue;
            }

            returns.Add((current - previous) / previous);
        }

        var averageReturn = returns.Any() ? returns.Average() : 0;
        var standardDeviation = returns.Count > 1 ? CalculateStandardDeviation(returns) : 0;
        var sharpeRatio = standardDeviation > 0 ? (float)(Math.Sqrt(252) * averageReturn / standardDeviation) : 0;

        var soldPositions = details.SelectMany(detail => detail.Sold ?? Enumerable.Empty<BacktestDayPosition>());
        var grossProfit = soldPositions.Where(position => position.Profit > 0).Sum(position => position.Profit);
        var grossLoss = soldPositions.Where(position => position.Profit < 0).Sum(position => MathF.Abs(position.Profit));
        var profitFactor = grossLoss > 0 ? grossProfit / grossLoss : grossProfit > 0 ? float.PositiveInfinity : 0;

        var maxDrawdown = CalculateMaxDrawdown(balances);

        return new PerformanceMetrics(
            averageDailyReturn: (float)averageReturn,
            dailyReturnStdDev: (float)standardDeviation,
            sharpeRatio: sharpeRatio,
            maxDrawdown: maxDrawdown,
            profitFactor: profitFactor);
    }

    private static float CalculateMaxDrawdown(IReadOnlyList<float> balances)
    {
        if (balances is null || balances.Count == 0)
        {
            return 0;
        }

        float peak = balances[0];
        float maxDrawdown = 0;

        foreach (var balance in balances)
        {
            if (balance > peak)
            {
                peak = balance;
            }

            if (peak <= 0)
            {
                continue;
            }

            var drawdown = (peak - balance) / peak;
            if (drawdown > maxDrawdown)
            {
                maxDrawdown = drawdown;
            }
        }

        return maxDrawdown;
    }

    private static double CalculateStandardDeviation(IReadOnlyCollection<double> values)
    {
        if (values is null || values.Count <= 1)
        {
            return 0;
        }

        var mean = values.Average();
        var sumOfSquares = values.Sum(value => Math.Pow(value - mean, 2));

        return Math.Sqrt(sumOfSquares / (values.Count - 1));
    }

    private readonly struct PerformanceMetrics
    {
        public PerformanceMetrics(
            float averageDailyReturn,
            float dailyReturnStdDev,
            float sharpeRatio,
            float maxDrawdown,
            float profitFactor)
        {
            AverageDailyReturn = averageDailyReturn;
            DailyReturnStdDev = dailyReturnStdDev;
            SharpeRatio = sharpeRatio;
            MaxDrawdown = maxDrawdown;
            ProfitFactor = profitFactor;
        }

        public float AverageDailyReturn { get; }
        public float DailyReturnStdDev { get; }
        public float SharpeRatio { get; }
        public float MaxDrawdown { get; }
        public float ProfitFactor { get; }

        public static PerformanceMetrics Empty => new(0, 0, 0, 0, 0);
    }

    #endregion
}
