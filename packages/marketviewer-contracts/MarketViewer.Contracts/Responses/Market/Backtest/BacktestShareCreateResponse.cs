using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Responses.Market.Backtest;

[ExcludeFromCodeCoverage]
public class BacktestShareCreateResponse
{
    public string ShareId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
