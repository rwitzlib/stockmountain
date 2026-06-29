using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Market.Scan;

[ExcludeFromCodeCoverage]
public class ScanRequest : BaseRequest, IRequest<OperationResult<ScanResponse>>
{
    public DateTimeOffset? Timestamp { get; set; }
    public List<string> Filters { get; set; }
}
