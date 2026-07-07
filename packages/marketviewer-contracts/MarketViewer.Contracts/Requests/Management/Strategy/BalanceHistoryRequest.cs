using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Management;

namespace MarketViewer.Contracts.Requests.Management.Strategy;

/// <summary>
/// Request to get balance history for a strategy within a date range.
/// </summary>
public class BalanceHistoryRequest
{
    public string StrategyId { get; set; }
    
    /// <summary>
    /// Start date in YYYY-MM-DD format. Defaults to 30 days ago.
    /// </summary>
    public string StartDate { get; set; }
    
    /// <summary>
    /// End date in YYYY-MM-DD format. Defaults to today.
    /// </summary>
    public string EndDate { get; set; }
}

