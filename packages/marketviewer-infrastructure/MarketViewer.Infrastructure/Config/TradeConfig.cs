using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Infrastructure.Config;

[ExcludeFromCodeCoverage]
public class TradeConfig
{
    public string TableName { get; set; }
    public string UserIndexName { get; set; }
    public string StrategyIndexName { get; set; }
}
