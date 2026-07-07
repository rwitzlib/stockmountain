using System.Diagnostics.CodeAnalysis;
using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Management;

namespace MarketViewer.Contracts.Requests.Management.Strategy;

[ExcludeFromCodeCoverage]
public class StrategyOptimizeRequest
{
    public string StrategyId { get; set; }
    public TradeType? Type { get; set; }
    public TradeStatus? Status { get; set; }
    public List<string> Filters { get; set; }
}



