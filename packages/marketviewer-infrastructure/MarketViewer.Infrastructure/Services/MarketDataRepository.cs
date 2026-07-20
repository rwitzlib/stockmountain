using MarketViewer.Contracts.Interfaces;
using MarketViewer.Contracts.Requests.Market;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Infrastructure.Mapping;
using Microsoft.Extensions.Logging;
using Massive.Client.Interfaces;

namespace MarketViewer.Infrastructure.Services
{
    public class MarketDataRepository(
        IMassiveClient massiveClient,
        ILogger<MarketDataRepository> logger) : IMarketDataRepository
    {
        public async Task<StocksResponse> GetStockDataAsync(StocksRequest request)
        {
            try
            {
                var aggregateRequest = AggregateMapper.ToMassiveRequest(request);

                var massiveResponse = await massiveClient.GetAggregates(aggregateRequest);

                var stocksResponse = AggregateMapper.ToStocksResponse(massiveResponse);

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
