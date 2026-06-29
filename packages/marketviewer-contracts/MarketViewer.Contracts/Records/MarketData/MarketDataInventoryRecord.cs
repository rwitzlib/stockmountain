using MarketViewer.Contracts.Enums;

namespace MarketViewer.Contracts.Records.MarketData;

public class MarketDataInventoryRecord
{
    public DateTimeOffset Date { get; set; }
    public int Multiplier { get; set; }
    public Timespan Timespan { get; set; }
    public string Bucket { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public MarketDataStatus Status { get; set; }
    public long? ObjectSize { get; set; }
    public string? ETag { get; set; }
    public int? RecordCount { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? RunId { get; set; }
    public string? Error { get; set; }
}
