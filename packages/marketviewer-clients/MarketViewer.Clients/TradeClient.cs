using MarketViewer.Clients.Interfaces;
using MarketViewer.Contracts.Records;
using MarketViewer.Contracts.Requests.Management.Trade;
using Microsoft.Extensions.Logging;

namespace MarketViewer.Clients;

/// <summary>
/// Client for Trade management operations
/// </summary>
public class TradeClient(HttpClient httpClient, ILogger<TradeClient> logger) : BaseMarketViewerClient(httpClient, logger), ITradeClient
{
    private const string BaseEndpoint = "api/trade";

    public async Task<bool> OpenAsync(TradeOpenRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug("Opening trade for strategy: {StrategyId}, ticker: {Ticker}", request.StrategyId, request.Ticker);
        return await PostBooleanAsync(BaseEndpoint, request, cancellationToken);
    }

    public async Task<IEnumerable<TradeRecord>?> ListAsync(TradeListRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug("Listing trades for user: {User}, strategy: {Strategy}", request.User, request.Strategy);
        
        // Build query string from request properties
        var queryParams = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(request.User))
        {
            queryParams.Add($"user={Uri.EscapeDataString(request.User)}");
        }
        
        if (!string.IsNullOrWhiteSpace(request.Strategy))
        {
            queryParams.Add($"strategy={Uri.EscapeDataString(request.Strategy)}");
        }

        if (request.Type.HasValue)
        {
            queryParams.Add($"type={Uri.EscapeDataString(request.Type.Value.ToString())}");
        }

        if (request.Status.HasValue)
        {
            queryParams.Add($"status={Uri.EscapeDataString(request.Status.Value.ToString())}");
        }

        var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        return await GetAsync<IEnumerable<TradeRecord>>($"{BaseEndpoint}{queryString}", cancellationToken);
    }

    public async Task<bool> CloseAsync(string id, TradeCloseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug("Closing trade with ID: {Id}", id);
        return await PutBooleanAsync($"{BaseEndpoint}/{id}", request, cancellationToken);
    }
} 