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
using System.Text.Json.Serialization;
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
    private readonly JsonSerializerOptions _options = new()
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
                var entry = MapEntry(record);

                if (record.Status == BacktestStatus.Completed)
                {
                    entry.HoldStats = TryDeserializeStats(record.HoldStatsJson, record.Id, "Hold");
                    entry.HighStats = TryDeserializeStats(record.HighStatsJson, record.Id, "High");
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

            var entry = MapEntry(record);

            if (record.Status == BacktestStatus.Completed)
            {
                entry.HoldStats = TryDeserializeStats(record.HoldStatsJson, record.Id, "Hold");
                entry.HighStats = TryDeserializeStats(record.HighStatsJson, record.Id, "High");
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

            var portfolio = await repository.GetPortfolioFromS3(record);

            if (portfolio is null)
            {
                return new OperationResult<BacktestResultResponse>
                {
                    Status = HttpStatusCode.NotFound,
                    ErrorMessages = ["No portfolio results found for this backtest."]
                };
            }

            return new OperationResult<BacktestResultResponse>
            {
                Status = HttpStatusCode.OK,
                Data = portfolio
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

    /// <summary>
    /// Stub for the future trade-universe exploration endpoint.
    /// Universe data is persisted at completion as universe.json; this handler is not implemented yet.
    /// </summary>
    public Task<OperationResult<IEnumerable<WorkerResponse>>> GetUniverse(string id)
    {
        logger.LogInformation("Universe endpoint stub called for backtest {id}", id);

        return Task.FromResult(new OperationResult<IEnumerable<WorkerResponse>>
        {
            Status = HttpStatusCode.NotImplemented,
            ErrorMessages =
            [
                "Trade universe exploration is not available yet. Use GET /backtest/result/{id} for the portfolio outcome."
            ]
        });
    }

    #region Private Methods

    private static BacktestEntryResponse MapEntry(BacktestContextRecord record) => new()
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

    private BacktestEntryStatsSummary TryDeserializeStats(string json, string id, string label)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BacktestEntryStatsSummary>(json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to deserialize {label}StatsJson for backtest {id}", label, id);
            return null;
        }
    }

    #endregion
}
