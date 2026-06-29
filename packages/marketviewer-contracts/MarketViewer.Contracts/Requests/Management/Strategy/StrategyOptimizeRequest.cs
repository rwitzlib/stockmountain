using System.Diagnostics.CodeAnalysis;
using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Management;
using MediatR;

namespace MarketViewer.Contracts.Requests.Management.Strategy;

[ExcludeFromCodeCoverage]
public class StrategyOptimizeRequest : IRequest<OperationResult<TradeResponse>>
{
    public string StrategyId { get; set; }
    public TradeType? Type { get; set; }
    public TradeStatus? Status { get; set; }
    public List<string> Filters { get; set; }
}



