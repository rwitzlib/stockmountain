using MarketViewer.Contracts.Enums;

namespace MarketViewer.Contracts.Records.MarketData;

public class MarketDataRunRecord
{
    public string RunId { get; set; } = string.Empty;
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public List<Timespan> Timespans { get; set; } = [];
    public int Multiplier { get; set; } = 1;
    public MarketDataStatus Status { get; set; }
    public string Source { get; set; } = string.Empty;
    public int RequestedCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Error { get; set; }
}
