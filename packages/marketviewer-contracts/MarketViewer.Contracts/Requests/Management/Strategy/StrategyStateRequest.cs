using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Management;

namespace MarketViewer.Contracts.Requests.Management.Strategy;

/// <summary>
/// Request to get the current state for a strategy.
/// </summary>
public class StrategyStateRequest
{
    public string StrategyId { get; set; }
}

