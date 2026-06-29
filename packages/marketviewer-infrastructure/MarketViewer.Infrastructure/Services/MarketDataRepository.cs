using MarketViewer.Contracts.Interfaces;
using MarketViewer.Contracts.Requests.Market;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Infrastructure.Mapping;
using Microsoft.Extensions.Logging;
using Polygon.Client.Interfaces;

namespace MarketViewer.Infrastructure.Services
{
    public class MarketDataRepository(
        IPolygonClient polygonClient,
        ILogger<MarketDataRepository> logger) : IMarketDataRepository
    {
        public async Task<StocksResponse> GetStockDataAsync(StocksRequest request)
        {
            try
            {
                var aggregateRequest = AggregateMapper.ToPolygonRequest(request);

                var polygonResponse = await polygonClient.GetAggregates(aggregateRequest);

                var stocksResponse = AggregateMapper.ToStocksResponse(polygonResponse);

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
