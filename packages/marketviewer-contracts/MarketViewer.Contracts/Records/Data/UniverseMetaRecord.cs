using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Records.Data;

[ExcludeFromCodeCoverage]
public class UniverseMetaRecord
{
    public string Version { get; set; }
    public int MaxId { get; set; }
    public int SymbolCount { get; set; }
    public string SnapshotKey { get; set; }
}
