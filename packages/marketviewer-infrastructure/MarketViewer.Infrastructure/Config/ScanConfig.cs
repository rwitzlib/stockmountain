using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Infrastructure.Config;

[ExcludeFromCodeCoverage]
public class ScanConfig
{
    public string TableName { get; set; }
    public int CadenceSec { get; set; }
}
