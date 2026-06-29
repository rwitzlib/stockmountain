namespace MarketViewer.Clients.Interfaces;

/// <summary>
/// Base interface for all MarketViewer API clients
/// </summary>
public interface IMarketViewerClient
{
    /// <summary>
    /// Sets the authorization token for API requests
    /// </summary>
    /// <param name="token">The JWT token</param>
    void SetAuthorizationToken(string token);
    
    /// <summary>
    /// Clears the authorization token
    /// </summary>
    void ClearAuthorizationToken();
} 