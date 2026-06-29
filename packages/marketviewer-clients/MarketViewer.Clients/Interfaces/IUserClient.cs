using MarketViewer.Contracts.Records;

namespace MarketViewer.Clients.Interfaces;

/// <summary>
/// Client interface for User management operations
/// </summary>
public interface IUserClient : IMarketViewerClient
{
    /// <summary>
    /// Gets a user by ID
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The user record</returns>
    Task<UserRecord?> GetAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Lists all users
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A collection of user records</returns>
    Task<IEnumerable<UserRecord>?> ListAsync(CancellationToken cancellationToken = default);
} 