using MarketViewer.Contracts.Models.Strategy;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Responses.Management;

[ExcludeFromCodeCoverage]
public class ScannerResponse
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string Name { get; set; }
    public StrategyEntrySettings EntrySettings { get; set; }
}
