using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Requests;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Tools;

[ExcludeFromCodeCoverage]
public class ToolsAggregateRequest : BaseRequest
{
    public string Ticker { get; set; }
    public Timespan Timespan { get; set; }
}
