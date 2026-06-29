using SchwabApi.Enums;
using System.Diagnostics.CodeAnalysis;

namespace SchwabApi.Requests;

[ExcludeFromCodeCoverage]
public class GetOrdersRequest
{
    public int? MaxResults { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public OrderStatus? Status { get; set; }
}
