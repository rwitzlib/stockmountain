using MarketViewer.Contracts.Requests.Management.Strategy;
using MarketViewer.Contracts.Responses.Management;

namespace MarketViewer.Clients.Interfaces;

/// <summary>
/// Client interface for Strategy management operations
/// </summary>
public interface IStrategyClient : IMarketViewerClient
{
    /// <summary>
    /// Creates a new strategy
    /// </summary>
    /// <param name="request">The strategy creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created strategy response</returns>
    Task<StrategyResponse?> CreateAsync(StrategyCreateRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a strategy by ID
    /// </summary>
    /// <param name="id">The strategy ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The strategy response</returns>
    Task<StrategyResponse?> GetAsync(string id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Lists strategies based on the provided criteria
    /// </summary>
    /// <param name="request">The list request criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A collection of strategy responses</returns>
    Task<IEnumerable<StrategyResponse>?> ListAsync(StrategyListRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates an existing strategy
    /// </summary>
    /// <param name="id">The strategy ID</param>
    /// <param name="request">The strategy update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated strategy response</returns>
    Task<StrategyResponse?> UpdateAsync(string id, StrategyUpdateRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a strategy
    /// </summary>
    /// <param name="id">The strategy ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deletion was successful</returns>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
} 