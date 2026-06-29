using MarketViewer.Contracts.Enums;

namespace MarketViewer.Contracts.Requests.MarketData;

public class MarketDataBackfillRequest
{
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public List<Timespan> Timespans { get; set; } = [Timespan.minute, Timespan.hour, Timespan.day];
    public int Multiplier { get; set; } = 1;
    public int MaxConcurrency { get; set; } = 3;
    public bool Overwrite { get; set; }
}
