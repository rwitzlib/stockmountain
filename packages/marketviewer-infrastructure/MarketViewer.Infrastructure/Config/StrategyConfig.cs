using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Infrastructure.Config;

[ExcludeFromCodeCoverage]
public class StrategyConfig
{
    public string TableName { get; set; }
    public string VisibilityIndexName { get; set; }
    public string UserIndexName { get; set; }
    public string StateIndexName { get; set; }
}
