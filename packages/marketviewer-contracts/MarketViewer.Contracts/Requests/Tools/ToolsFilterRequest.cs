using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Responses.Tools;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Tools;

[ExcludeFromCodeCoverage]
public class ToolsFilterRequest : BaseRequest
{
    public string Ticker { get; set; }
    public int Multiplier { get; set; }
    public Timespan Timespan { get; set; }
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
    public List<string> Filters { get; set; }
}
