using MarketViewer.Contracts.Models.Strategy;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace MarketViewer.Contracts.Requests.Management.Scanner;

[ExcludeFromCodeCoverage]
public class ScannerUpdateRequest
{
    [IgnoreDataMember]
    public string Id { get; set; }
    public string Name { get; set; }
    public StrategyEntrySettings EntrySettings { get; set; }
}
