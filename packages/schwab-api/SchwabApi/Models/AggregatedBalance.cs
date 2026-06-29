using System.Diagnostics.CodeAnalysis;

namespace SchwabApi.Models;

[ExcludeFromCodeCoverage]
public class AggregatedBalance
{
    public float CurrentLiquidationValue { get; set; }
    public float LiquidationValue { get; set; }
}
