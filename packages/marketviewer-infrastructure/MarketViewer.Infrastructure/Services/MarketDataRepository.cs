using AutoMapper;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Interfaces;
using MarketViewer.Contracts.Requests.Market;
using MarketViewer.Contracts.Responses.Market;
using Microsoft.Extensions.Logging;
using Polygon.Client.Interfaces;
using Polygon.Client.Models;
using Polygon.Client.Requests;
using Polygon.Client.Responses;
using System.Net;

namespace MarketViewer.Infrastructure.Services
{
    public class MarketDataRepository(
        IPolygonClient polygonClient,
        IMapper mapper,
        ILogger<MarketDataRepository> logger) : IMarketDataRepository
    {
        public async Task<StocksResponse> GetStockDataAsync(StocksRequest request)
        {
            try
            {
                var aggregateRequest = mapper.Map<StocksRequest, PolygonAggregateRequest>(request);

                var polygonResponse = await polygonClient.GetAggregates(aggregateRequest);

                var stocksResponse = mapper.Map<PolygonAggregateResponse, StocksResponse>(polygonResponse);

                return stocksResponse;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error retrieving stock data: {ex.Message}");
                return null;
            }
        }
    }
}
