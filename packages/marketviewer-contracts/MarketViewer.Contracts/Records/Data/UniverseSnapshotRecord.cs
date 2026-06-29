using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Records.Data;

[ExcludeFromCodeCoverage]
public class UniverseSnapshotRecord
{
    public string Version { get; set; }
    public int MaxId { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public string IdToSymbolBlob { get; set; }
    public string[] Symbols { get; set; }
}
