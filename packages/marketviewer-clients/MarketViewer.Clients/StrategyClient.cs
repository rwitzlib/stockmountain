using MarketViewer.Clients.Interfaces;
using MarketViewer.Contracts.Requests.Management.Strategy;
using MarketViewer.Contracts.Responses.Management;
using Microsoft.Extensions.Logging;

namespace MarketViewer.Clients;

/// <summary>
/// Client for Strategy management operations
/// </summary>
public class StrategyClient(HttpClient httpClient, ILogger<StrategyClient> logger) : BaseMarketViewerClient(httpClient, logger), IStrategyClient
{
    private const string BaseEndpoint = "api/strategy";

    public async Task<StrategyResponse?> CreateAsync(StrategyCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug("Creating strategy with name: {Name}", request.Name);
        return await PostAsync<StrategyResponse>(BaseEndpoint, request, cancellationToken);
    }

    public async Task<StrategyResponse?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        _logger.LogDebug("Getting strategy with ID: {Id}", id);
        return await GetAsync<StrategyResponse>($"{BaseEndpoint}/{id}", cancellationToken);
    }

    public async Task<IEnumerable<StrategyResponse>?> ListAsync(StrategyListRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug("Listing strategies");
        
        // Build query string from request properties
        var queryParams = new List<string>();
        
        // Use reflection to get all properties and their values for query string
        var properties = typeof(StrategyListRequest).GetProperties();
        foreach (var prop in properties)
        {
            var value = prop.GetValue(request);
            if (value != null)
            {
                queryParams.Add($"{prop.Name.ToLower()}={Uri.EscapeDataString(value.ToString()!)}");
            }
        }

        var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        return await GetAsync<IEnumerable<StrategyResponse>>($"{BaseEndpoint}{queryString}", cancellationToken);
    }

    public async Task<StrategyResponse?> UpdateAsync(string id, StrategyUpdateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug("Updating strategy with ID: {Id}", id);
        return await PutAsync<StrategyResponse>($"{BaseEndpoint}/{id}", request, cancellationToken);
    }

    public new async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        _logger.LogDebug("Deleting strategy with ID: {Id}", id);
        return await DeleteAsync($"{BaseEndpoint}/{id}", cancellationToken);
    }
} 