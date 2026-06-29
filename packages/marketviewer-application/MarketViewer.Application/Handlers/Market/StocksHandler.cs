using MediatR;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MarketViewer.Application.Services;
using MarketViewer.Contracts.Interfaces;
using System.Linq;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Requests.Market;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Enums;
using Polygon.Client.Models;
using MarketViewer.Contracts.Models.Indicator;
using Microsoft.Extensions.Logging;

namespace MarketViewer.Application.Handlers.Market;

public class StocksHandler(
    IMarketDataRepository repository,
    IMarketCache marketCache,
    IIndicatorCalculationService indicatorCalculationService,
    ILogger<StocksHandler> logger) : IRequestHandler<StocksRequest, OperationResult<StocksResponse>>
{
    public async Task<OperationResult<StocksResponse>> Handle(StocksRequest request, CancellationToken cancellationToken)
    {
        if (!ValidateAggregateRequest(request, out var errorMessages))
        {
            logger.LogInformation("Invalid StocksRequest received: {Errors}", string.Join("; ", errorMessages));
            return new OperationResult<StocksResponse>
            {
                Status = HttpStatusCode.BadRequest,
                ErrorMessages = errorMessages
            };
        }

        StocksResponse response;

        // Check if we should use cache: minute timespan and date range within last 5 days
        //if (ShouldUseCache(request))
        //{

        //    var timeframe = new Timeframe(1, request.Timespan);
        //    var cacheResponse = marketCache.GetStocksResponse(request.Ticker, timeframe, DateTimeOffset.Now);
            
        //    // If cache miss, fall back to repository
        //    if (cacheResponse is null)
        //    {
        //        response = await repository.GetStockDataAsync(request);
        //    }
        //    else
        //    {
        //        response = cacheResponse.Clone();
        //    }

        //    if (request.To.Date == DateTimeOffset.Now.Date)
        //    {
        //        // If the request is for today's data, we need to ensure we have the latest live bar
        //        var latestBar = marketCache.GetLiveBar(request.Ticker);

        //        TryAddBarToResponse(request.Multiplier, request.Timespan, latestBar, response);
        //    }
        //}
        //else
        //{
            response = await repository.GetStockDataAsync(request);
        //}

        if (response is null)
        {
            errorMessages.Add("Query returned invalid result.");

            return new OperationResult<StocksResponse>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = errorMessages
            };
        }

        if (response.Results is null || response.Results.Count() == 0)
        {
            errorMessages.Add("Query returned no results.");

            return new OperationResult<StocksResponse>
            {
                Status = HttpStatusCode.InternalServerError,
                ErrorMessages = errorMessages
            };
        }

        if (request.Indicators is not null)
        {
            var studies = new List<IndicatorResponse>();
            var timeframe = new Timeframe(request.Multiplier, request.Timespan);

            foreach (var indicator in request.Indicators)
            {
                var studyResponse = indicatorCalculationService.Compute(indicator, response, timeframe);

                if (studyResponse is not null && studyResponse.Results.Count > 0)
                {
                    if (request.Limit <= studyResponse.Results.Count)
                    {
                        studyResponse.Results = studyResponse.Results.TakeLast(request.Limit).ToList();
                    }
                    studies.Add(studyResponse);
                }
            }

            response.Indicators = studies;
        }

        response.Results = response.Results.TakeLast(request.Limit).ToList();

        return new OperationResult<StocksResponse>
        {
            Status = HttpStatusCode.OK,
            Data = response
        };
    }

    #region Private Methods

    private static bool ValidateAggregateRequest(StocksRequest request, out List<string> errorMessages)
    {
        errorMessages = [];

        if (string.IsNullOrWhiteSpace(request.Ticker))
        {
            errorMessages.Add("Must include a valid Ticker.");
        }

        if (request.From > DateTimeOffset.Now)
        {
            errorMessages.Add($"'From' date must be earlier than {DateTimeOffset.Now:yyyy-MM-dd}.");
        }

        if (request.From < DateTimeOffset.UnixEpoch)
        {
            errorMessages.Add($"'From' date must be later than {DateTimeOffset.UnixEpoch:yyyy-MM-dd}.");
        }

        if (request.From > request.To)
        {
            errorMessages.Add("'From' date must be earlier than 'To' date.");
        }

        request.To = request.To.AddHours(23).AddMinutes(59);

        return errorMessages.Count == 0;
    }

    #endregion
}
