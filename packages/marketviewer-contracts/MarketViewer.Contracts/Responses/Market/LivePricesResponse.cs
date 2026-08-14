using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Responses.Market;

[ExcludeFromCodeCoverage]
public class LivePricesResponse
{
    /// <summary>
    /// Latest live price per requested ticker. Tickers with no live data are omitted
    /// so callers can fall back to another price source.
    /// </summary>
    public List<LivePrice> Prices { get; set; } = [];
}

[ExcludeFromCodeCoverage]
public class LivePrice
{
    public string Ticker { get; set; }

    public float Price { get; set; }

    /// <summary>
    /// Minute-aligned timestamp (ms epoch, data time) of the bar the price came from.
    /// Callers on a delayed plan should staleness-check this against their data clock,
    /// not the wall clock.
    /// </summary>
    public long Timestamp { get; set; }

    /// <summary>
    /// True when the price is the in-progress websocket bar's latest tick rather than
    /// a completed bar's close.
    /// </summary>
    public bool FromFormingBar { get; set; }
}
