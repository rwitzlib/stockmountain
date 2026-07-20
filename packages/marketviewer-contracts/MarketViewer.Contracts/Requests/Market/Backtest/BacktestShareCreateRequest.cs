using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Market.Backtest;

[ExcludeFromCodeCoverage]
public class BacktestShareCreateRequest
{
    /// <summary>When false (the default) the strategy configuration is redacted to counts.</summary>
    public bool IncludeConfig { get; set; }

    /// <summary>Optional display title for the shared page.</summary>
    public string Title { get; set; }
}
