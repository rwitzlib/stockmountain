using MarketViewer.Contracts.Records;
using MarketViewer.Contracts.Requests.Management.Trade;

namespace MarketViewer.Clients.Interfaces;

/// <summary>
/// Client interface for Trade management operations
/// </summary>
public interface ITradeClient : IMarketViewerClient
{
    /// <summary>
    /// Opens a new trade
    /// </summary>
    /// <param name="request">The trade open request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the trade was opened successfully</returns>
    Task<bool> OpenAsync(TradeOpenRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Lists trades based on the provided criteria
    /// </summary>
    /// <param name="request">The list request criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A collection of trade records</returns>
    Task<IEnumerable<TradeRecord>?> ListAsync(TradeListRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Closes an existing trade
    /// </summary>
    /// <param name="id">The trade ID</param>
    /// <param name="request">The trade close request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the trade was closed successfully</returns>
    Task<bool> CloseAsync(string id, TradeCloseRequest request, CancellationToken cancellationToken = default);
} 