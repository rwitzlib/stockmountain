using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Models.Strategy;
using MarketViewer.Contracts.Responses.Management;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace MarketViewer.Contracts.Requests.Management.Strategy;

[ExcludeFromCodeCoverage]
public class StrategyUpdateRequest
{
    [IgnoreDataMember]
    public string Id { get; set; }
    public string Name { get; set; }
    public StrategyStateType State { get; set; }
    public VisibilityType Visibility { get; set; }
    public TradeType Type { get; set; }
    public IntegrationType Integration { get; set; }
    public StrategyPositionSettings PositionSettings { get; set; }
    public StrategyExitSettings ExitSettings { get; set; }
    public StrategyEntrySettings EntrySettings { get; set; }
}
