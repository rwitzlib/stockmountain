using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Models.Strategy;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MarketViewer.Contracts.Dtos;

[ExcludeFromCodeCoverage]
public class StrategyDto
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string Name { get; set; }
    public StrategyStateType State { get; set; }
    public VisibilityType Visibility { get; set; }
    public TradeType Type { get; set; }
    public IntegrationType Integration { get; set; }
    public StrategyPositionSettings PositionSettings { get; set; }
    public StrategyExitSettings ExitSettings { get; set; }
    public StrategyEntrySettings EntrySettings { get; set; }
    public int? Expiry { get; set; }

    public string ComputeStrategyHash()
    {
        return EntrySettings?.ComputeStrategyHash() ?? null;
    }
}
