using System.Diagnostics.CodeAnalysis;
using MarketViewer.Contracts.Models.Indicator;
using Polygon.Client.Models;

namespace MarketViewer.Contracts.Responses.Market;

[ExcludeFromCodeCoverage]
public class StocksResponse
{
    /// <summary>
    /// The exchange symbol that this item is traded under.
    /// </summary>
    public string Ticker { get; set; }

    /// <summary>
    /// The status of this request's response.
    /// </summary>
    public string Status { get; set; }

    public List<Bar> Results { get; set; }

    public List<IndicatorResponse> Indicators { get; set; }
    public Information TickerInfo { get; set; } = new();

    public class Information
    {
        public TickerDetails TickerDetails { get; set; } = new();
        public float DailyVolume { get; set; }
        public float AverageVolume { get; set; }
    }

    public StocksResponse Clone()
    {
        var response = (StocksResponse)MemberwiseClone();
        response.Results = response.Results.Select(bar => bar.Clone()).ToList();
        // TODO: add cloning for studies as well later on?
        return response;
    }
}
