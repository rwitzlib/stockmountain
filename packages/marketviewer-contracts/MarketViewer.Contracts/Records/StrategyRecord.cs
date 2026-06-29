using MarketViewer.Contracts.Enums.Strategy;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Records;

[ExcludeFromCodeCoverage]
public class StrategyRecord
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string Name { get; set; }
    public bool Enabled { get; set; }
    public TradeType Type { get; set; }
    public IntegrationType Integration { get; set; }
    public string PositionInfo { get; set; }
    public string ExitInfo { get; set; }
    public string Argument { get; set; }
}
