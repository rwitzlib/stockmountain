using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Records.Scan;

[ExcludeFromCodeCoverage]
public class ScanRecord
{
    public string StrategyHash { get; set; }
    public long Window { get; set; }
    public List<string> Tickers { get; set; }
    public long TimeElapsed { get; set; }
    public int CadenceSec { get; set; }
}
