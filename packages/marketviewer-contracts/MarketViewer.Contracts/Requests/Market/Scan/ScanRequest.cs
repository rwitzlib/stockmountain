using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Market.Scan;

[ExcludeFromCodeCoverage]
public class ScanRequest : BaseRequest
{
    public DateTimeOffset? Timestamp { get; set; }
    public List<string> Filters { get; set; }
}
