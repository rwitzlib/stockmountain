using MarketViewer.Contracts.Enums;

namespace MarketViewer.Contracts.Requests.MarketData;

public class MarketDataInventoryQueryRequest
{
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public Timespan? Timespan { get; set; }
    public int? Multiplier { get; set; }
}
