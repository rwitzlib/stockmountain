using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Tools;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Tools;

[ExcludeFromCodeCoverage]
public class ToolsScanRequest : BaseRequest
{
    public string Ticker { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    //public ScanArgument Argument { get; set; }
}
