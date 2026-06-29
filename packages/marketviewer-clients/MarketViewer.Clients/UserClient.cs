using MarketViewer.Clients.Interfaces;
using MarketViewer.Contracts.Records;
using Microsoft.Extensions.Logging;

namespace MarketViewer.Clients;

/// <summary>
/// Client for User management operations
/// </summary>
public class UserClient(HttpClient httpClient, ILogger<UserClient> logger) : BaseMarketViewerClient(httpClient, logger), IUserClient
{
    private const string BaseEndpoint = "api/user";

    public async Task<UserRecord?> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        _logger.LogDebug("Getting user with ID: {UserId}", userId);
        return await GetAsync<UserRecord>($"{BaseEndpoint}/{userId}", cancellationToken);
    }

    public async Task<IEnumerable<UserRecord>?> ListAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Listing all users");
        return await GetAsync<IEnumerable<UserRecord>>(BaseEndpoint, cancellationToken);
    }
} 