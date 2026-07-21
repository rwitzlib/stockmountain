using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Management;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Management.Trade;

[ExcludeFromCodeCoverage]
public class TradeListRequest
{
    // All filters are optional; with <Nullable>enable</Nullable> they must be declared
    // nullable or ASP.NET model validation treats them as required query parameters.
    public string? Strategy { get; set; }
    public string? User { get; set; }
    public TradeType? Type { get; set; }
    public TradeStatus? Status { get; set; }
}
