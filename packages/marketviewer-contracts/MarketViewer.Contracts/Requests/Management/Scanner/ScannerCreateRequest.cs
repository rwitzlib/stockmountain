using MarketViewer.Contracts.Models.Strategy;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Management.Scanner;

[ExcludeFromCodeCoverage]
public class ScannerCreateRequest
{
    public string Name { get; set; }
    public StrategyEntrySettings EntrySettings { get; set; }
}
