using Amazon.SQS.Model;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Records.Data;
using MarketViewer.Contracts.Requests.Data.Ticker;
using MarketViewer.Contracts.Responses.Data.Ticker;
using MarketViewer.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Polygon.Client.Interfaces;
using Polygon.Client.Models;
using Polygon.Client.Requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace MarketViewer.Application.Handlers.Data.Tickers;

public class TickerHandler(
    IPolygonClient polygonClient,
    MetaRepository metaRepository,
    ILogger<TickerHandler> logger)
{
    public async Task<OperationResult<UniverseSnapshotRecord>> GetSnapshot(string version = null)
    {
        try
        {
            var meta = await metaRepository.GetUniverseMeta(version);

            if (meta == null)
            {
                return new OperationResult<UniverseSnapshotRecord>
                {
                    Status = HttpStatusCode.NotFound,
                    ErrorMessages = ["Universe meta not found."]
                };
            }

            var snapshot = await metaRepository.GetUniverseMetaSnapshot(meta);

            return new OperationResult<UniverseSnapshotRecord>
            {
                Status = HttpStatusCode.OK,
                Data = snapshot
            };
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving universe snapshot for version {Version}: {Message}", version, e.Message);
            return new OperationResult<UniverseSnapshotRecord>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["Internal Error."]
            };
        }
    }

    public async Task<OperationResult<TickerPopulateResponse>> Populate(TickerPopulateRequest request)
    {
        try
        {
            var meta = await metaRepository.GetUniverseMeta();

            meta ??= new UniverseMetaRecord
            {
                SymbolCount = 0,
                Version = "INITIAL",
                MaxId = 0,
                SnapshotKey = "VERSION#0"
            };

            var snapshot = await metaRepository.GetUniverseMetaSnapshot(meta);

            List<TickerDetails> tickerDetailsList = [];

            for (int i = 0; i < request.Markets.Count; i++)
            {
                for (int j = 0; j < request.Types.Count; j++)
                {
                    var tickersResponse = await polygonClient.GetTickers(new PolygonGetTickersRequest
                    {
                        Market = request.Markets[i],
                        Active = request.Active,
                        Type = request.Types[j]
                    });
                    if (tickersResponse?.Results != null)
                    {
                        tickerDetailsList.AddRange(tickersResponse.Results);
                    }
                }
            }

            var now = DateTimeOffset.Now;
            var maxId = snapshot?.MaxId ?? 0;

            List<SecurityMasterRecord> recordsToCreateOrUpdate = [];
            foreach (var tickerResult in tickerDetailsList)
            {
                if (snapshot is null || !snapshot.Symbols.Contains(tickerResult.Ticker))
                {
                    maxId++;
                    recordsToCreateOrUpdate.Add(new SecurityMasterRecord
                    {
                        Symbol = tickerResult.Ticker,
                        Id = maxId,
                        Active = true,
                        UpdatedAt = now,
                        Type = tickerResult.Type,
                        Market = tickerResult.Market
                    });
                }
                else
                {
                    recordsToCreateOrUpdate.Add(new SecurityMasterRecord
                    {
                        Symbol = tickerResult.Ticker,
                        Active = true,
                        UpdatedAt = now,
                        Type = tickerResult.Type,
                        Market = tickerResult.Market
                    });
                }
            }

            List<SecurityMasterRecord> updatedRecords = [];
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 10
            };
            await Parallel.ForEachAsync(recordsToCreateOrUpdate, parallelOptions, async (record, cancellationToken) =>
            {
                var updatedRecord = await metaRepository.PutSecurityRecord(record);

                if (updatedRecord != null)
                {
                    lock (updatedRecords)
                    {
                        updatedRecords.Add(updatedRecord);
                    }
                }
            });

            var newSnapshot = new UniverseSnapshotRecord
            {
                Version = $"VERSION#{now:yyyy-MM-dd}",
                GeneratedAt = now,
                MaxId = maxId,
                Symbols = new string[maxId + 1],
            };

            for (int i = 0; i < maxId; i++)
            {
                newSnapshot.Symbols[i] = updatedRecords.FirstOrDefault(r => r.Id == i)?.Symbol ?? string.Empty;
            }

            await metaRepository.CreateUniverseSnapshotRecord(newSnapshot);

            var universeMeta = new UniverseMetaRecord
            {
                Version = now.ToString("yyyy-MM-dd"),
                MaxId = maxId,
                SnapshotKey = $"VERSION#{now:yyyy-MM-dd}",
                SymbolCount = updatedRecords.Count
            };

            await metaRepository.UpdateUniverseMeta(universeMeta);

            return new OperationResult<TickerPopulateResponse>
            {
                Status = HttpStatusCode.OK,
                Data = null
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error populating tickers: {Message}", ex.Message);
            return new OperationResult<TickerPopulateResponse>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["Internal Error."]
            };
        }
    }
}
