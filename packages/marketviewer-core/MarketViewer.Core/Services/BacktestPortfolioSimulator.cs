using MarketViewer.Contracts.Models.Backtest;
using MarketViewer.Contracts.Models.Strategy;
using MarketViewer.Contracts.Responses.Market.Backtest;

namespace MarketViewer.Core.Services;

/// <summary>
/// Runs capital-constrained portfolio simulation over unconstrained worker trade results.
/// Shared by the backtest orchestrator (persist-at-completion) and any callers that need the same logic.
/// </summary>
public static class BacktestPortfolioSimulator
{
    private static readonly TimeZoneInfo EasternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    public static BacktestResultResponse Simulate(
        string backtestId,
        float creditsUsed,
        DateTimeOffset startDate,
        StrategyPositionSettings positionSettings,
        IEnumerable<WorkerResponse> entries,
        bool includeOther = false)
    {
        var entryList = entries?.Where(e => e is not null).ToList() ?? [];
        var dayRange = GetDateRange(startDate, entryList).ToList();

        var hold = SimulateStrategy("hold", positionSettings, entryList, dayRange);
        var high = SimulateStrategy("high", positionSettings, entryList, dayRange);
        BacktestStrategyPortfolio other = null;

        if (includeOther)
        {
            other = SimulateStrategy("other", positionSettings, entryList, dayRange);
        }

        return new BacktestResultResponse
        {
            Id = backtestId,
            CreditsUsed = creditsUsed,
            Hold = hold,
            High = high,
            Other = other
        };
    }

    private static BacktestStrategyPortfolio SimulateStrategy(
        string type,
        StrategyPositionSettings positionSettings,
        List<WorkerResponse> entries,
        List<DateTimeOffset> dayRange)
    {
        var availableFunds = positionSettings.StartingBalance;
        var openPositions = new List<BacktestEntryResultCollection>();
        var equity = new List<BacktestEquityPoint>();
        var trades = new List<BacktestExecutedTrade>();
        var maxConcurrent = 0;
        var totalTradesTaken = 0;

        foreach (var day in dayRange)
        {
            var offset = EasternTimeZone.IsDaylightSavingTime(day.Date)
                ? TimeSpan.FromHours(-4)
                : TimeSpan.FromHours(-5);
            var marketOpen = new DateTimeOffset(day.Year, day.Month, day.Day, 9, 30, 0, offset);
            var marketClose = new DateTimeOffset(day.Year, day.Month, day.Day, 16, 0, 0, offset);

            var entry = entries.FirstOrDefault(q =>
                q.Date.ToString("yyyy-MM-dd") == day.ToString("yyyy-MM-dd"));

            var startCash = availableFunds;
            var dayProfit = 0f;
            var tradesTakenToday = 0;
            var dayMaxConcurrent = openPositions.Count;

            for (var i = 0; i < (marketClose - marketOpen).TotalMinutes; i++)
            {
                var currentTime = marketOpen.AddMinutes(i);

                SellPositions(type, openPositions, currentTime, ref availableFunds, trades, ref dayProfit);
                BuyPositions(
                    type,
                    entry,
                    currentTime,
                    positionSettings,
                    ref availableFunds,
                    openPositions,
                    ref tradesTakenToday);

                dayMaxConcurrent = Math.Max(dayMaxConcurrent, openPositions.Count);
            }

            maxConcurrent = Math.Max(maxConcurrent, dayMaxConcurrent);
            totalTradesTaken += tradesTakenToday;

            equity.Add(new BacktestEquityPoint
            {
                Date = day,
                StartCash = startCash,
                EndCash = availableFunds,
                TotalBalance = openPositions.Sum(q => q.StartPosition) + availableFunds,
                OpenPositions = openPositions.Count,
                MaxConcurrentPositions = dayMaxConcurrent,
                DayProfit = dayProfit,
                TradesTaken = tradesTakenToday
            });
        }

        var wins = trades.Where(t => t.Profit > 0).ToList();
        var losses = trades.Where(t => t.Profit < 0).ToList();
        var performance = CalculatePerformanceMetrics(equity, trades, positionSettings.StartingBalance);

        // End balance should reflect cash plus any still-open marked positions.
        var endBalance = availableFunds + openPositions.Sum(q => q.StartPosition);

        return new BacktestStrategyPortfolio
        {
            Stats = new BacktestEntryStats
            {
                EndBalance = endBalance,
                BalanceChange = endBalance - positionSettings.StartingBalance,
                WinRatio = wins.Count + losses.Count > 0
                    ? (float)wins.Count / (wins.Count + losses.Count)
                    : 0,
                AvgWin = wins.Count > 0 ? wins.Average(q => q.Profit) : 0,
                AvgLoss = losses.Count > 0 ? losses.Average(q => q.Profit) : 0,
                MaxConcurrentPositions = maxConcurrent,
                TotalTradesTaken = totalTradesTaken,
                AverageDailyReturn = performance.AverageDailyReturn,
                DailyReturnStdDev = performance.DailyReturnStdDev,
                SharpeRatio = performance.SharpeRatio,
                MaxDrawdown = performance.MaxDrawdown,
                ProfitFactor = performance.ProfitFactor
            },
            Equity = equity,
            Trades = trades
        };
    }

    private static void SellPositions(
        string type,
        List<BacktestEntryResultCollection> openPositions,
        DateTimeOffset timestamp,
        ref float availableFunds,
        List<BacktestExecutedTrade> trades,
        ref float dayProfit)
    {
        var positionsToRemove = new List<BacktestEntryResultCollection>();

        foreach (var position in openPositions)
        {
            var outcome = GetOutcome(type, position);
            if (outcome is null || outcome.SoldAt != timestamp)
            {
                continue;
            }

            availableFunds += outcome.EndPosition;
            dayProfit += outcome.Profit;

            trades.Add(new BacktestExecutedTrade
            {
                Ticker = position.Ticker,
                BoughtAt = position.BoughtAt,
                SoldAt = outcome.SoldAt,
                StartPrice = position.StartPrice,
                EndPrice = outcome.EndPrice,
                Shares = position.Shares,
                StartPosition = position.StartPosition,
                EndPosition = outcome.EndPosition,
                Profit = outcome.Profit,
                MaxRunup = outcome.MaxRunup,
                MaxDrawdown = outcome.MaxDrawdown,
                StoppedOut = outcome.StoppedOut,
                ExitReason = outcome.ExitReason
            });

            positionsToRemove.Add(position);
        }

        foreach (var position in positionsToRemove)
        {
            openPositions.Remove(position);
        }
    }

    private static void BuyPositions(
        string type,
        WorkerResponse entry,
        DateTimeOffset timestamp,
        StrategyPositionSettings positionSettings,
        ref float availableFunds,
        List<BacktestEntryResultCollection> openPositions,
        ref int tradesTakenToday)
    {
        if (entry?.Results is null)
        {
            return;
        }

        // Tie-break same-minute signals by ticker so which trade wins a contested
        // funds/concurrency slot does not depend on upstream list order.
        var candidates = entry.Results
            .Where(r => r.BoughtAt == timestamp)
            .OrderBy(r => r.Ticker, StringComparer.Ordinal);

        foreach (var result in candidates)
        {
            if (GetOutcome(type, result) is null)
            {
                continue;
            }

            if (availableFunds < positionSettings.Model.Size)
            {
                continue;
            }

            if (positionSettings.MaxConcurrentPositions > 0
                && openPositions.Count >= positionSettings.MaxConcurrentPositions)
            {
                continue;
            }

            openPositions.Add(result);
            availableFunds -= result.StartPosition;
            tradesTakenToday++;
        }
    }

    private static BacktestEntryResult GetOutcome(string type, BacktestEntryResultCollection position) =>
        type.ToLowerInvariant() switch
        {
            "hold" => position.Hold,
            "high" => position.High,
            "other" => position.Other,
            _ => throw new NotImplementedException($"Unknown strategy type: {type}")
        };

    private static IEnumerable<DateTimeOffset> GetDateRange(
        DateTimeOffset startDate,
        IEnumerable<WorkerResponse> entries)
    {
        var entriesWithDates = entries.Where(q => q.Results is not null && q.Results.Any()).ToList();

        if (entriesWithDates.Count == 0)
        {
            return [];
        }

        var lastDates = new List<DateTimeOffset>
        {
            entriesWithDates.Max(q => q.Results.Max(result => result.Hold?.SoldAt ?? DateTimeOffset.MinValue)),
            entriesWithDates.Max(q => q.Results.Max(result => result.High?.SoldAt ?? DateTimeOffset.MinValue)),
        };

        var otherDates = entriesWithDates
            .SelectMany(q => q.Results)
            .Where(r => r.Other is not null)
            .Select(r => r.Other.SoldAt)
            .ToList();

        if (otherDates.Count > 0)
        {
            lastDates.Add(otherDates.Max());
        }

        var maxDate = lastDates.Where(d => d > DateTimeOffset.MinValue).DefaultIfEmpty(startDate).Max();

        return Enumerable.Range(0, (maxDate - startDate).Days + 1)
            .Select(day => startDate.AddDays(day))
            .Where(day => day.DayOfWeek is not DayOfWeek.Sunday and not DayOfWeek.Saturday);
    }

    private static PerformanceMetrics CalculatePerformanceMetrics(
        IReadOnlyList<BacktestEquityPoint> equity,
        IEnumerable<BacktestExecutedTrade> trades,
        float startingBalance)
    {
        if (equity is null || equity.Count == 0)
        {
            return PerformanceMetrics.Empty;
        }

        var balances = new List<float> { startingBalance };
        balances.AddRange(equity.Select(point =>
            point.TotalBalance != 0
                ? point.TotalBalance
                : point.EndCash != 0
                    ? point.EndCash
                    : point.StartCash));

        var returns = new List<double>();
        for (var i = 1; i < balances.Count; i++)
        {
            var previous = balances[i - 1];
            var current = balances[i];

            if (previous == 0)
            {
                continue;
            }

            returns.Add((current - previous) / previous);
        }

        var averageReturn = returns.Count > 0 ? returns.Average() : 0;
        var standardDeviation = returns.Count > 1 ? CalculateStandardDeviation(returns) : 0;
        var sharpeRatio = standardDeviation > 0
            ? (float)(Math.Sqrt(252) * averageReturn / standardDeviation)
            : 0;

        var soldPositions = trades?.ToList() ?? [];
        var grossProfit = soldPositions.Where(t => t.Profit > 0).Sum(t => t.Profit);
        var grossLoss = soldPositions.Where(t => t.Profit < 0).Sum(t => MathF.Abs(t.Profit));
        var profitFactor = grossLoss > 0
            ? grossProfit / grossLoss
            : grossProfit > 0 ? float.PositiveInfinity : 0;

        return new PerformanceMetrics(
            averageDailyReturn: (float)averageReturn,
            dailyReturnStdDev: (float)standardDeviation,
            sharpeRatio: sharpeRatio,
            maxDrawdown: CalculateMaxDrawdown(balances),
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
}
