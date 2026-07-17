using Amazon.Lambda.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Text.Json;
using MarketViewer.Contracts.Enums.Backtest;
using MarketViewer.Contracts.Models.Backtest;
using Backtest.Lambda.Services;
using MarketViewer.Contracts.Requests.Market.Backtest;
using MarketViewer.Core.Services;
using MarketViewer.Contracts.Responses.Market.Backtest;
using Microsoft.Extensions.Logging;

namespace Backtest.Lambda;

public class OrchestratorFunction(IServiceProvider serviceProvider)
{
    public IServiceProvider ServiceProvider => serviceProvider;

    private readonly IBacktestRepository _backtestRepository = serviceProvider.GetService<IBacktestRepository>();
    private readonly IUserRepository _userRepository = serviceProvider.GetService<IUserRepository>();
    private readonly BacktestWorkerService _workerService = serviceProvider.GetService<BacktestWorkerService>();
    private readonly ILogger<OrchestratorFunction> _logger = serviceProvider.GetService<ILogger<OrchestratorFunction>>();

    private const int ESTIMATED_DAILY_CREDIT_COST = 120;

    public OrchestratorFunction() : this(Startup.ConfigureServices()) { }

    public async Task FunctionHandler(OrchestratorRequest request, ILambdaContext context)
    {
        using var logScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["BacktestId"] = request.Id,
            ["BacktestStartDate"] = request.Start.ToString("yyyy-MM-dd"),
            ["BacktestEndDate"] = request.End.ToString("yyyy-MM-dd"),
            ["AwsRequestId"] = context.AwsRequestId,
            ["LambdaFunction"] = context.FunctionName
        });

        try
        {
            var sp = Stopwatch.StartNew();

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
                _logger.LogInformation(
                    "Insufficient credits for user {UserId} to run backtest. Estimated cost: {EstimatedCost}, Available credits: {AvailableCredits}",
                    request.UserId, estimatedCreditCost, user?.Credits ?? 0);

                record.Status = BacktestStatus.Failed;
                record.CreditsUsed = 0;
                record.Errors = ["Insufficient credits to run backtest. Please purchase more credits."];
                await _backtestRepository.Put(record);
                return;
            }

            record.Status = BacktestStatus.InProgress;
            await _backtestRepository.Put(record);

            var entries = await _workerService.GetBacktestResultsFromLambda(request);

            _logger.LogInformation(
                "Found {EntryCount} entries for the backtest request ID {RequestId} after {ElapsedSeconds} seconds.",
                entries.Count, request.Id, sp.Elapsed.TotalSeconds);

            var relevantEntries = entries
                .Where(q => q.Date >= request.Start.Date && q.Date <= request.End.Date)
                .ToList();

            if (relevantEntries.Count == 0)
            {
                _logger.LogInformation("No relevant entries found for the given date range.");
                record.Status = BacktestStatus.Failed;
                record.CreditsUsed = 0;
                record.Errors = ["No results found for the given date range."];
                await _backtestRepository.Put(record, entries);
                return;
            }

            _logger.LogInformation("Found {EntryCount} relevant entries for the backtest request.", relevantEntries.Count);

            var workerErrors = CollectWorkerErrors(relevantEntries);

            // Failed days come back as placeholders with empty results; if nothing at all
            // produced a trade and there were errors, the run failed rather than "no signals".
            if (workerErrors is not null && relevantEntries.All(q => q.Results is null || q.Results.Count == 0))
            {
                _logger.LogError("Backtest {RequestId} produced no results and reported errors.", request.Id);
                record.Status = BacktestStatus.Failed;
                record.CreditsUsed = 0;
                record.Errors = workerErrors;
                await _backtestRepository.Put(record, entries);
                return;
            }

            var creditsUsed = relevantEntries.Sum(result => result.CreditsUsed);
            var includeOther = request.ExitSettings?.ConditionalExit is not null;

            var portfolio = BacktestPortfolioSimulator.Simulate(
                backtestId: request.Id,
                creditsUsed: creditsUsed,
                startDate: request.Start,
                positionSettings: request.PositionSettings,
                entries: relevantEntries,
                includeOther: includeOther);

            record.Status = BacktestStatus.Completed;
            record.CreditsUsed = creditsUsed;
            record.Errors = workerErrors;
            record.HoldProfit = portfolio.Hold.Stats.BalanceChange;
            record.HighProfit = portfolio.High.Stats.BalanceChange;
            record.ConditionalProfit = portfolio.Other?.Stats.BalanceChange ?? 0;
            record.DurationSeconds = (int)sp.Elapsed.TotalSeconds;
            record.Request = new BacktestCreateRequest
            {
                Start = request.Start,
                End = request.End,
                PositionSettings = request.PositionSettings,
                EntrySettings = request.EntrySettings,
                ExitSettings = request.ExitSettings,
            };
            record.HoldStatsJson = JsonSerializer.Serialize(ToSummary(portfolio.Hold.Stats));
            record.HighStatsJson = JsonSerializer.Serialize(ToSummary(portfolio.High.Stats));

            await _backtestRepository.PutCompleted(record, portfolio, relevantEntries);

            var creditsDebited = await _userRepository.TryDebitCredits(record.UserId, creditsUsed);
            if (!creditsDebited)
            {
                _logger.LogWarning(
                    "Backtest {RequestId} completed but credits could not be debited for user {UserId}",
                    request.Id, record.UserId);

                record.Status = BacktestStatus.Failed;
                record.Errors = ["Unable to settle credits for this backtest. Please try again."];
                await _backtestRepository.Put(record);
                return;
            }

            _logger.LogInformation(
                "Backtest completed successfully for request ID {RequestId}. Total time taken: {ElapsedSeconds} ms. Credits used: {CreditsUsed}",
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

    /// <summary>
    /// Rolls per-day worker errors (dropped signals, failed days) up onto the record so
    /// partial data is visible to the user instead of silently missing. Returns null when
    /// every day ran clean; entries are distinct and capped for the DynamoDB string set.
    /// </summary>
    private static List<string> CollectWorkerErrors(IEnumerable<WorkerResponse> entries)
    {
        const int MaxErrors = 25;

        var errors = entries
            .Where(entry => entry.Errors is { Count: > 0 })
            .OrderBy(entry => entry.Date)
            .SelectMany(entry => entry.Errors
                .Where(error => !string.IsNullOrWhiteSpace(error))
                .Select(error => $"{entry.Date:yyyy-MM-dd}: {error}"))
            .Distinct()
            .ToList();

        if (errors.Count == 0)
        {
            return null;
        }

        if (errors.Count > MaxErrors)
        {
            errors = errors
                .Take(MaxErrors)
                .Append($"... and {errors.Count - MaxErrors} more")
                .ToList();
        }

        return errors;
    }

    private static BacktestEntryStatsSummary ToSummary(BacktestEntryStats stats) => new()
    {
        WinRatio = stats.WinRatio,
        ProfitFactor = stats.ProfitFactor,
        TotalTradesTaken = stats.TotalTradesTaken,
        MaxDrawdown = stats.MaxDrawdown,
        SharpeRatio = stats.SharpeRatio
    };
}
