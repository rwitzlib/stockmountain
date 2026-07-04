using Amazon.Lambda.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Diagnostics;
using System.Net;
using MarketViewer.Contracts.Enums.Backtest;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Models.Backtest;
using Backtest.Lambda.Repository;
using MarketViewer.Contracts.Requests.Market.Backtest;
using MarketViewer.Contracts.Responses.Market.Backtest;
using MarketViewer.Contracts.Records;
using Microsoft.Extensions.Logging;
using MarketViewer.Contracts.Models.Strategy;

namespace Backtest.Lambda;

public class OrchestratorFunction(IServiceProvider serviceProvider)
{
    public IServiceProvider ServiceProvider => serviceProvider; // Expose the service provider for testing purposes

    private readonly BacktestRepository _backtestRepository = serviceProvider.GetService<BacktestRepository>();
    private readonly UserRepository _userRepository = serviceProvider.GetService<UserRepository>();
    private readonly ILogger<OrchestratorFunction> _logger = serviceProvider.GetService<ILogger<OrchestratorFunction>>();

    private readonly TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    private const int ESTIMATED_DAILY_CREDIT_COST = 120; // Estimated Credit Cost per Day

    public OrchestratorFunction() : this(Startup.ConfigureServices()) { }

    public async Task FunctionHandler(OrchestratorRequest request, ILambdaContext context)
    {
        try
        {
            var sp = new Stopwatch();
            sp.Start();

            var record = await _backtestRepository.Get(request.Id);

            _logger.LogInformation("Processing backtest request with ID {RequestId}.", request.Id);

            if (record is null || record.Status is not BacktestStatus.Pending)
            {
                _logger.LogInformation("Backtest record not found or already completed for request ID {RequestId}.", request.Id);

                if (record is not null)
                {
                    record.Status = BacktestStatus.Failed;
                    record.CreditsUsed = 0;
                    record.Errors = ["Backtest already completed or not found. Please try again."];

                    await _backtestRepository.Put(record);
                }
                return;
            }

            var estimatedCreditCost = ((request.End - request.Start).Days + 1) * ESTIMATED_DAILY_CREDIT_COST;

            var user = await _userRepository.Get(record.UserId);
            if (user == null || user.Credits < estimatedCreditCost)
            {
                _logger.LogInformation("Insufficient credits for user {UserId} to run backtest. Estimated cost: {EstimatedCost}, Available credits: {AvailableCredits}",
                    request.UserId, estimatedCreditCost, user?.Credits ?? 0);

                // TODO: In the future, check if there are S3 results for these request details and if so, check if the
                // user has enough credits to run backtest, excluding days from S3 results.
                record.Status = BacktestStatus.Failed;
                record.CreditsUsed = 0;
                record.Errors = ["Insufficient credits to run backtest. Please purchase more credits."];

                await _backtestRepository.Put(record);
                return;
            }

            record.Status = BacktestStatus.InProgress;
            await _backtestRepository.Put(record);
            
            var entries = await _backtestRepository.GetBacktestResultsFromLambda(request);

            _logger.LogInformation("Found {EntryCount} entries for the backtest request ID {RequestId} after {ElapsedSeconds} seconds.", entries.Count, request.Id, sp.Elapsed.TotalSeconds);

            _logger.LogInformation("Filtering entries for the date range from {StartDate} to {EndDate}.  Found date: {entry}.", request.Start, request.End, entries.FirstOrDefault()?.Date);
            var relevantEntries = entries.Where(q => q.Date >= request.Start.Date && q.Date <= request.End.Date);

            if (relevantEntries is null || !relevantEntries.Any())
            {
                _logger.LogInformation("No relevant entries found for the given date range.");
                record.Status = BacktestStatus.Failed;
                record.CreditsUsed = 0;
                record.Errors = ["No results found for the given date range."];

                await _backtestRepository.Put(record, entries);
                return;
            }

            _logger.LogInformation("Found {EntryCount} relevant entries for the backtest request.", relevantEntries.Count());

            var availableFundsHold = request.PositionSettings.StartingBalance;
            var availableFundsHigh = request.PositionSettings.StartingBalance;
            //var availableFundsOther = request.PositionSettings.StartingBalance;

            var holdOpenPositions = new List<BacktestEntryResultCollection>();
            var highOpenPositions = new List<BacktestEntryResultCollection>();
            //var otherOpenPositions = new List<BacktestEntryResultCollection>();

            int maxConcurrentHoldPositions = 0;
            int maxConcurrentHighPositions = 0;
            //int maxConcurrentOtherPositions = 0;

            var dayRange = GetDateRange(request.Start, relevantEntries);

            var backtestDayResults = new List<BacktestDayResultV3>();

            foreach (var day in dayRange)
            {
                var offset = TimeZone.IsDaylightSavingTime(day.Date) ? TimeSpan.FromHours(-4) : TimeSpan.FromHours(-5);
                var marketOpen = new DateTimeOffset(day.Year, day.Month, day.Day, 9, 30, 0, offset);
                var marketClose = new DateTimeOffset(day.Year, day.Month, day.Day, 16, 0, 0, offset);

                var entry = relevantEntries.FirstOrDefault(q => q.Date == day.Date);

                var backtestEntryDay = new BacktestDayResultV3
                {
                    Date = day,
                    Hold = new BacktestDayDetails
                    {
                        StartCashAvailable = availableFundsHold,
                        Bought = [],
                        Sold = []
                    },
                    High = new BacktestDayDetails
                    {
                        StartCashAvailable = availableFundsHigh,
                        Bought = [],
                        Sold = []
                    },
                    //Other = request.ExitInfo.Other is null ? null : new BacktestDayDetails
                    //{
                    //    StartCashAvailable = availableFundsOther,
                    //    Bought = [],
                    //    Sold = []
                    //}
                };

                int dayMaxHoldPositions = 0;
                int dayMaxHighPositions = 0;

                for (int i = 0; i < (marketClose - marketOpen).TotalMinutes; i++)
                {
                    var currentTime = marketOpen.AddMinutes(i);

                    SellPositionIfApplicable("hold", holdOpenPositions, currentTime, ref availableFundsHold, backtestEntryDay);
                    //if (request.ExitInfo.Other is not null)
                    //{
                    //    SellPositionIfApplicable("other", otherOpenPositions, currentTime, ref availableFundsOther, backtestEntryDay);
                    //}
                    SellPositionIfApplicable("high", highOpenPositions, currentTime, ref availableFundsHigh, backtestEntryDay);

                    BuyPositionIfApplicable("hold", entry, currentTime, request.PositionSettings, ref availableFundsHold, holdOpenPositions, backtestEntryDay);
                    //if (request.ExitInfo.Other is not null)
                    //{
                    //    BuyPositionIfApplicable("other", entry, currentTime, parameters, ref availableFundsOther, otherOpenPositions, backtestEntryDay);
                    //}
                    BuyPositionIfApplicable("high", entry, currentTime, request.PositionSettings, ref availableFundsHigh, highOpenPositions, backtestEntryDay);

                    dayMaxHoldPositions = Math.Max(dayMaxHoldPositions, holdOpenPositions.Count);
                    dayMaxHighPositions = Math.Max(dayMaxHighPositions, highOpenPositions.Count);
                }

                maxConcurrentHoldPositions = Math.Max(maxConcurrentHoldPositions, dayMaxHoldPositions);
                maxConcurrentHighPositions = Math.Max(maxConcurrentHighPositions, dayMaxHighPositions);

                backtestEntryDay.Hold.EndCashAvailable = availableFundsHold;
                backtestEntryDay.Hold.TotalBalance = holdOpenPositions.Sum(q => q.StartPosition) + backtestEntryDay.Hold.EndCashAvailable;
                backtestEntryDay.Hold.OpenPositions = holdOpenPositions.Count;
                backtestEntryDay.Hold.MaxConcurrentPositions = dayMaxHoldPositions;
                backtestEntryDay.Hold.TradesTaken = backtestEntryDay.Hold.Bought.Count;
                backtestEntryDay.Hold.Profit = backtestEntryDay.Hold.Sold.Sum(q => q.Profit);

                //if (request.ExitInfo.Other is not null)
                //{
                //    backtestEntryDay.Other.EndCashAvailable = availableFundsOther;
                //    backtestEntryDay.Other.TotalBalance = otherOpenPositions.Sum(q => q.StartPosition) + backtestEntryDay.Other.EndCashAvailable;
                //}

                backtestEntryDay.High.EndCashAvailable = availableFundsHigh;
                backtestEntryDay.High.TotalBalance = highOpenPositions.Sum(q => q.StartPosition) + backtestEntryDay.High.EndCashAvailable;
                backtestEntryDay.High.OpenPositions = highOpenPositions.Count;
                backtestEntryDay.High.MaxConcurrentPositions = dayMaxHighPositions;
                backtestEntryDay.High.TradesTaken = backtestEntryDay.High.Bought.Count;
                backtestEntryDay.High.Profit = backtestEntryDay.High.Sold.Sum(q => q.Profit);

                backtestDayResults.Add(backtestEntryDay);
            }

            var holdWins = backtestDayResults.SelectMany(q => q.Hold.Sold).Where(q => q.Profit > 0);
            var holdLosses = backtestDayResults.SelectMany(q => q.Hold.Sold).Where(q => q.Profit < 0);

            //var otherWins = request.ExitSettings.ConditionalExit is null ? [] : backtestDayResults.SelectMany(q => q.Other.Sold).Where(q => q.Profit > 0);
            //var otherLosses = request.ExitSettings.ConditionalExit is null ? [] : backtestDayResults.SelectMany(q => q.Other.Sold).Where(q => q.Profit < 0);

            var highWins = backtestDayResults.SelectMany(q => q.High.Sold).Where(q => q.Profit > 0);
            var highLosses = backtestDayResults.SelectMany(q => q.High.Sold).Where(q => q.Profit < 0);

            var response = new OperationResult<BacktestResultResponse>
            {
                Status = HttpStatusCode.OK,
                Data = new BacktestResultResponse
                {
                    Id = request.Id,
                    Hold = new BacktestEntryStats
                    {
                        EndBalance = availableFundsHold,
                        BalanceChange = availableFundsHold - request.PositionSettings.StartingBalance,
                        WinRatio = holdWins.Any() ? (float)holdWins.Count() / (float)(holdWins.Count() + holdLosses.Count()) : 0,
                        AvgWin = holdWins.Any() ? holdWins.Average(q => q.Profit) : 0,
                        AvgLoss = holdLosses.Any() ? holdLosses.Average(q => q.Profit) : 0,
                        MaxConcurrentPositions = maxConcurrentHoldPositions
                    },
                    High = new BacktestEntryStats
                    {
                        EndBalance = availableFundsHigh,
                        BalanceChange = availableFundsHigh - request.PositionSettings.StartingBalance,
                        WinRatio = highWins.Any() ? (float)highWins.Count() / (float)(highWins.Count() + highLosses.Count()) : 0,
                        AvgWin = highWins.Any() ? highWins.Average(q => q.Profit) : 0,
                        AvgLoss = highLosses.Any() ? highLosses.Average(q => q.Profit) : 0,
                        MaxConcurrentPositions = maxConcurrentHighPositions
                    },
                    //Other = request.ExitInfo.Other is null ? null : new BacktestEntryStats
                    //{
                    //    EndBalance = availableFundsOther,
                    //    BalanceChange = availableFundsOther - request.PositionSettings.StartingBalance,
                    //    WinRatio = otherWins.Any() ? (float)otherWins.Count() / (float)(otherWins.Count() + otherLosses.Count()) : 0,
                    //    AvgWin = otherWins.Any() ? otherWins.Average(q => q.Profit) : 0,
                    //    AvgLoss = otherLosses.Any() ? otherLosses.Average(q => q.Profit) : 0,
                    //    MaxConcurrentPositions = backtestDayResults.Any() ? backtestDayResults.Max(result => result.Other.OpenPositions) : 0
                    //},
                    CreditsUsed = relevantEntries.Where(result => result is not null).Sum(result => result.CreditsUsed),
                    Results = backtestDayResults,
                    Entries = relevantEntries
                }
            };

            record.Status = BacktestStatus.Completed;
            record.CreditsUsed = response.Data.CreditsUsed;
            record.HoldProfit = response.Data.Hold.BalanceChange;
            record.HighProfit = response.Data.High.BalanceChange;
            //record.ConditionalProfit = response.Data.?.BalanceChange ?? null;
            record.DurationSeconds = (int)sp.Elapsed.TotalSeconds;
            record.Request = new BacktestCreateRequest
            {
                Start = request.Start,
                End = request.End,
                PositionSettings = request.PositionSettings,
                EntrySettings = request.EntrySettings,
                ExitSettings = request.ExitSettings,
            };

            await _backtestRepository.Put(record, relevantEntries);

            var creditsDebited = await _userRepository.TryDebitCredits(record.UserId, response.Data.CreditsUsed);
            if (!creditsDebited)
            {
                _logger.LogWarning("Backtest {RequestId} completed but credits could not be debited for user {UserId}", request.Id, record.UserId);

                record.Status = BacktestStatus.Failed;
                record.Errors = ["Unable to settle credits for this backtest. Please try again."];
                await _backtestRepository.Put(record);
                return;
            }

            _logger.LogInformation("Backtest completed successfully for request ID {RequestId}. Total time taken: {ElapsedSeconds} ms. Credits used: {CreditsUsed}",
                request.Id, sp.Elapsed.TotalSeconds, record.CreditsUsed);

            sp.Stop();
        }
        catch (Exception e)
        {
            var record = await _backtestRepository.Get(request.Id);

            context.Logger.LogError(e, "An error occurred while processing the backtest request: {Message}", e.Message);

            if (record is not null)
            {
                record.Status = BacktestStatus.Failed;
                record.Errors = ["An error occurred while processing the backtest request. Please try again later."];
                await _backtestRepository.Put(record);
            }
        }
    }

    #region Private Methods

    private static IEnumerable<DateTimeOffset> GetDateRange(DateTimeOffset startDate, IEnumerable<WorkerResponse> entries)
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

        var maxDate = lastDates.Max().Date;

        return Enumerable.Range(0, (maxDate - startDate).Days + 1)
            .Select(day => startDate.AddDays(day))
            .Where(day => day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday);
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
                    break;

                //case "other":
                //    backtestDay.Other.Sold.Add(new BacktestDayPosition
                //    {
                //        Ticker = position.Ticker,
                //        Price = position.Other.EndPrice,
                //        Shares = position.Shares,
                //        Position = position.Other.EndPosition,
                //        Profit = position.Other.Profit,
                //        Timestamp = position.Other.SoldAt,
                //        StoppedOut = position.Other.StoppedOut
                //    });
                //    break;

                default: throw new NotImplementedException();
            }
            ;

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
            if (availableFunds < positionSettings.Model.Size || openPositions.Count >= positionSettings.MaxConcurrentPositions)
            {
                continue;
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
                    break;
                case "high":
                    backtestDay.High.Bought.Add(backtestDayPosition);
                    break;
                //case "other":
                //    backtestDay.Other.Bought.Add(backtestDayPosition);
                //    break;
                default:
                    throw new NotImplementedException();
            }

            openPositions.Add(result);
            availableFunds -= result.StartPosition;
        }
    }

    #endregion
}
