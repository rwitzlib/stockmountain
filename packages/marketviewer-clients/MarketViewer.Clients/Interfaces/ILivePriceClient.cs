using MarketViewer.Contracts.Requests.Market;
using MarketViewer.Contracts.Responses.Market;

namespace MarketViewer.Clients.Interfaces;

/// <summary>
/// Client interface for the internal live-price endpoint.
/// </summary>
public interface ILivePriceClient : IMarketViewerClient
{
    /// <summary>
    /// Fetches the latest live price for a batch of tickers. Returns null when the
    /// request fails; tickers without live data are omitted from the response.
    /// </summary>
    Task<LivePricesResponse?> GetPricesAsync(LivePricesRequest request, CancellationToken cancellationToken = default);
}
