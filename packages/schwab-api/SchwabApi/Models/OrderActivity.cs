using System.Diagnostics.CodeAnalysis;

namespace SchwabApi.Models;

[ExcludeFromCodeCoverage]
public class OrderActivity
{
    public long ActivityId { get; set; }
    public string ActivityType { get; set; }
    public string ExecutionType { get; set; }
    public float Quantity { get; set; }
    public float OrderRemainingQuantity { get; set; }
    public List<ExecutionLeg> ExecutionLegs { get; set; }
}
