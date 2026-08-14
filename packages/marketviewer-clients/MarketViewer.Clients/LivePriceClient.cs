using MarketViewer.Clients.Interfaces;
using MarketViewer.Contracts.Requests.Market;
using MarketViewer.Contracts.Responses.Market;
using Microsoft.Extensions.Logging;

namespace MarketViewer.Clients;

/// <summary>
/// Client for the internal live-price endpoint (shared-secret bearer auth).
/// </summary>
public class LivePriceClient(HttpClient httpClient, ILogger<LivePriceClient> logger) : BaseMarketViewerClient(httpClient, logger), ILivePriceClient
{
    private const string BaseEndpoint = "api/live/prices";

    public async Task<LivePricesResponse?> GetPricesAsync(LivePricesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await PostAsync<LivePricesResponse>(BaseEndpoint, request, cancellationToken);
    }
}
