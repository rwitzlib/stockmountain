using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Requests.Tools;
using MarketViewer.Contracts.Responses.Tools;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;

namespace MarketViewer.Application.Handlers.Tools;

public class SnapshotHandler(IMemoryCache memoryCache, ILogger<SnapshotHandler> logger)
{
    public OperationResult<SnapshotResponse> GetSnapshot(SnapshotRequest request)
    {
        try
        {
            var tickerList = request.Tickers.Split(',');

            if (tickerList.Length == 0)
            {
                return new OperationResult<SnapshotResponse>
                {
                    Status = HttpStatusCode.BadRequest,
                    ErrorMessages = ["No tickers provided."]
                };
            }
            
            var snapshotResponse = memoryCache.Get<SnapshotResponse>("snapshot");

            return new OperationResult<SnapshotResponse>
            {
                Status = HttpStatusCode.OK,
                Data = new SnapshotResponse
                {
                    Entries = snapshotResponse?.Entries.Where(q => tickerList.Contains(q.Ticker)).ToList() ?? []
                }
            };
        }
        catch (Exception e)
        {
            logger.LogInformation("Error getting snapshot: {Message}", e.Message);
            return new OperationResult<SnapshotResponse>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = ["Internal error."]
            };
        }
    }
}
