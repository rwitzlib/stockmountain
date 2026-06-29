using Polygon.Client.Models;
using MarketViewer.Contracts.Responses.Market;
using Polygon.Client.Responses;
using MarketViewer.Contracts.Models;

namespace MarketViewer.Contracts.Caching;

public interface IMarketCache
{
    Task<IEnumerable<StocksResponse>> Initialize(DateTimeOffset date, Timeframe timeframe);

    IEnumerable<string> GetTickers();
    void SetTickers(IEnumerable<string> tickers);

    IEnumerable<string> GetTickersByTimeframe(Timeframe timeframe, DateTimeOffset timestamp);
    void SetTickersByTimeframe(DateTimeOffset date, Timeframe timeframe, IEnumerable<string> tickers);

    StocksResponse GetStocksResponse(string ticker, Timeframe timeframe, DateTimeOffset timestamp);
    void SetStocksResponse(StocksResponse stocksResponse, Timeframe timeframe, DateTimeOffset date);

    TickerDetails GetTickerDetails(string ticker);
    void SetTickerDetails(TickerDetails tickerDetails);

    void AddLiveBar(PolygonWebsocketAggregateResponse bar);
    Bar GetLiveBar(string ticker);
}
