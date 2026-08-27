using MarketViewer.Contracts.Models.Strategy;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Dtos;

[ExcludeFromCodeCoverage]
public class ScannerDto
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string Name { get; set; }
    public StrategyEntrySettings EntrySettings { get; set; }
}
