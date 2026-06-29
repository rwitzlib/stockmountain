using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Infrastructure.Config;

[ExcludeFromCodeCoverage]
public class MetaConfig
{
    public string SecurityMasterTableName { get; set; }
    public string MetaTableName { get; set; }
}
