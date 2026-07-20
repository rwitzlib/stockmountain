using MarketViewer.Contracts.Enums.Backtest;
using MarketViewer.Contracts.Interfaces;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Models.Backtest;
using MarketViewer.Contracts.Requests.Market;
using MarketViewer.Contracts.Requests.Market.Backtest;
using MarketViewer.Contracts.Responses.Market.Backtest;
using MarketViewer.Core.Auth;
using MarketViewer.Core.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Timespan = MarketViewer.Contracts.Enums.Timespan;

namespace MarketViewer.Application.Handlers.Market.Backtest;

/// <summary>
/// Creates and serves public share snapshots of completed backtests. The snapshot is a
/// self-contained payload in S3 (shares/{shareId}.json, expired by lifecycle rule) so
/// anonymous viewers never touch authed endpoints. Redaction happens here, at compose
/// time — the stored payload is public and must never contain masked config values,
/// the owner's user id, or the backtest id.
/// </summary>
public class BacktestShareHandler(
    AuthContext authContext,
    IBacktestRepository repository,
    IMarketDataRepository marketDataRepository,
    ILogger<BacktestShareHandler> logger)
{
    private const string BenchmarkTicker = "SPY";
    private const int ShareLifetimeDays = 30;
    private const int MaxTitleLength = 100;

    public async Task<OperationResult<BacktestShareCreateResponse>> CreateShare(string backtestId, BacktestShareCreateRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(backtestId))
            {
                return new OperationResult<BacktestShareCreateResponse>
                {
                    Status = HttpStatusCode.BadRequest,
                    ErrorMessages = ["Invalid backtest ID."]
                };
            }

            var record = await repository.Get(backtestId);

            // Same response whether missing or someone else's — don't leak existence.
            if (record is null || record.UserId != authContext.UserId)
            {
                return new OperationResult<BacktestShareCreateResponse>
                {
                    Status = HttpStatusCode.NotFound,
                    ErrorMessages = ["Backtest not found."]
                };
            }

            if (record.Status != BacktestStatus.Completed)
            {
                return new OperationResult<BacktestShareCreateResponse>
                {
                    Status = HttpStatusCode.BadRequest,
                    ErrorMessages = ["Backtest is not completed yet."]
                };
            }

            var portfolio = await repository.GetPortfolioFromS3(record);

            if (portfolio is null)
            {
                return new OperationResult<BacktestShareCreateResponse>
                {
                    Status = HttpStatusCode.NotFound,
                    ErrorMessages = ["No portfolio results found for this backtest."]
                };
            }

            // The backtest id grants access via the authed backtest endpoints; keep it out
            // of the public payload.
            portfolio.Id = null;
            portfolio.CreditsUsed = 0;

            var start = DateTimeOffset.Parse(record.Start);
            var end = DateTimeOffset.Parse(record.End);
            var createdAt = DateTimeOffset.UtcNow;

            var payload = new BacktestSharePayload
            {
                CreatedAt = createdAt,
                ExpiresAt = createdAt.AddDays(ShareLifetimeDays),
                Title = NormalizeTitle(request?.Title),
                Start = start,
                End = end,
                Config = BuildConfig(record.Request, request?.IncludeConfig == true),
                Result = portfolio,
                Benchmark = await TryGetBenchmark(start, end)
            };

            var shareId = GenerateShareId();

            if (!await repository.PutShare(shareId, payload))
            {
                return new OperationResult<BacktestShareCreateResponse>
                {
                    Status = HttpStatusCode.InternalServerError,
                    ErrorMessages = ["Failed to create share."]
                };
            }

            logger.LogInformation("Created share {shareId} for backtest {backtestId} (masked: {masked})",
                shareId, backtestId, payload.Config.Masked);

            return new OperationResult<BacktestShareCreateResponse>
            {
                Status = HttpStatusCode.OK,
                Data = new BacktestShareCreateResponse
                {
                    ShareId = shareId,
                    ExpiresAt = payload.ExpiresAt
                }
            };
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating share for backtest {backtestId}", backtestId);
            return new OperationResult<BacktestShareCreateResponse>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["Internal error."]
            };
        }
    }

    public async Task<OperationResult<string>> GetShareJson(string shareId)
    {
        try
        {
            var json = await repository.GetShareJson(shareId);

            if (json is null)
            {
                return new OperationResult<string>
                {
                    Status = HttpStatusCode.NotFound,
                    ErrorMessages = ["Share not found."]
                };
            }

            return new OperationResult<string>
            {
                Status = HttpStatusCode.OK,
                Data = json
            };
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error reading share {shareId}", shareId);
            return new OperationResult<string>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["Internal error."]
            };
        }
    }

    #region Private Methods

    private static string GenerateShareId()
    {
        // 128 bits of crypto randomness, base64url (22 chars) — never derived from the backtest id.
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var trimmed = title.Trim();
        return trimmed.Length <= MaxTitleLength ? trimmed : trimmed[..MaxTitleLength];
    }

    private static BacktestShareConfig BuildConfig(BacktestCreateRequest request, bool includeConfig)
    {
        if (includeConfig)
        {
            return new BacktestShareConfig
            {
                Masked = false,
                PositionSettings = request?.PositionSettings,
                ExitSettings = request?.ExitSettings,
                EntrySettings = request?.EntrySettings
            };
        }

        return new BacktestShareConfig
        {
            Masked = true,
            EntryFilterCount = request?.EntrySettings?.Filters?.Count ?? 0,
            HasStopLoss = request?.ExitSettings?.StopLoss is { Value: > 0 },
            HasProfitTarget = request?.ExitSettings?.TakeProfit is { Value: > 0 },
            HasTimedExit = request?.ExitSettings?.TimedExit?.Timeframe is not null
        };
    }

    private async Task<List<BacktestShareBenchmarkPoint>> TryGetBenchmark(DateTimeOffset start, DateTimeOffset end)
    {
        try
        {
            var response = await marketDataRepository.GetStockDataAsync(new StocksRequest
            {
                Ticker = BenchmarkTicker,
                Multiplier = 1,
                Timespan = Timespan.day,
                From = start,
                To = end.AddHours(23).AddMinutes(59)
            });

            if (response?.Results is null || !response.Results.Any())
            {
                logger.LogWarning("No {ticker} bars returned for share benchmark window {start} - {end}",
                    BenchmarkTicker, start, end);
                return null;
            }

            return response.Results
                .OrderBy(bar => bar.Timestamp)
                .Select(bar => new BacktestShareBenchmarkPoint
                {
                    Date = DateTimeOffset.FromUnixTimeMilliseconds(bar.Timestamp),
                    Close = bar.Close
                })
                .ToList();
        }
        catch (Exception e)
        {
            // A share without a benchmark overlay is still a valid share.
            logger.LogWarning(e, "Failed to fetch {ticker} benchmark for share", BenchmarkTicker);
            return null;
        }
    }

    #endregion
}
