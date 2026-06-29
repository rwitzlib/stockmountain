using MarketViewer.Contracts.Enums;
using System.Diagnostics.CodeAnalysis;

namespace MarketDataAggregator;

[ExcludeFromCodeCoverage]
public class MarketDataOrchestratorRequest
{
    public string RunId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public List<Timespan> Timespans { get; set; } = [Timespan.minute, Timespan.hour, Timespan.day];
    public int Multiplier { get; set; } = 1;
    public int MaxConcurrency { get; set; } = 3;
    public bool Overwrite { get; set; }
    public string Source { get; set; } = "backfill";
}
