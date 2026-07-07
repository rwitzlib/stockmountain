using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Management;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Management.Strategy;

[ExcludeFromCodeCoverage]
public class StrategyReadRequest
{
    public string Id { get; set; }
}
