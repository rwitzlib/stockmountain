using MarketViewer.Contracts.Records.MarketData;
using MarketViewer.Contracts.Requests.MarketData;

namespace MarketViewer.Contracts.Interfaces;

public interface IMarketDataCatalogRepository
{
    Task<MarketDataInventoryRecord> PutInventoryRecord(MarketDataInventoryRecord record);
    Task<List<MarketDataInventoryRecord>> ListInventory(MarketDataInventoryQueryRequest request);
    Task<MarketDataRunRecord> PutRunRecord(MarketDataRunRecord record);
    Task<List<MarketDataRunRecord>> ListRuns(int limit = 50);
}
