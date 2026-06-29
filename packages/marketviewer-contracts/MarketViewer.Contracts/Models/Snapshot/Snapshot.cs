using Polygon.Client.Models;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models.Snapshot;

[ExcludeFromCodeCoverage]
public class Snapshot
{
    public long Timestamp { get; set; }
    public DateTimeOffset DateTime { get; set; }
    public Bar Minute { get; set; }
    public Bar Hour { get; set; }
}
