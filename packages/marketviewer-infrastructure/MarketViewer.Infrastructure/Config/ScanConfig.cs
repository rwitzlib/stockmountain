using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Infrastructure.Config;

[ExcludeFromCodeCoverage]
public class ScanConfig
{
    public string TableName { get; set; }
    public int CadenceSec { get; set; }

    /// <summary>
    /// When true (the default), strategy entry scans evaluate completed minute bars
    /// only, matching backtest semantics, and the signal window becomes the
    /// data-clock minute so re-scans of the same bar dedupe in the executor.
    /// See ADR 0003.
    /// </summary>
    public bool CompletedBarEntries { get; set; } = true;
}
