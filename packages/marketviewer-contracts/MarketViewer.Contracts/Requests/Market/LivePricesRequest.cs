using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Market;

[ExcludeFromCodeCoverage]
public class LivePricesRequest
{
    /// <summary>
    /// Ticker symbols to fetch the latest live price for.
    /// </summary>
    public List<string> Tickers { get; set; } = [];
}
