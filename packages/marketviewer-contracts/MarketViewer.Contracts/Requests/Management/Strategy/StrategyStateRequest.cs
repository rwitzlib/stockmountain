using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Management;
using MediatR;

namespace MarketViewer.Contracts.Requests.Management.Strategy;

/// <summary>
/// Request to get the current state for a strategy.
/// </summary>
public class StrategyStateRequest : IRequest<OperationResult<StrategyStateResponse>>
{
    public string StrategyId { get; set; }
}

