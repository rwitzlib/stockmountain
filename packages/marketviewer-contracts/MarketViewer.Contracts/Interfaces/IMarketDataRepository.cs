using MarketViewer.Contracts.Requests.Market;
using MarketViewer.Contracts.Responses.Market;

namespace MarketViewer.Contracts.Interfaces
{
    public interface IMarketDataRepository
    {
        public Task<StocksResponse> GetStockDataAsync(StocksRequest request);

    }
}
