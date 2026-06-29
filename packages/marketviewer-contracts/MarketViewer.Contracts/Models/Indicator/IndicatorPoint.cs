using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models.Indicator;

[ExcludeFromCodeCoverage]
public class IndicatorPoint
{
    public long Timestamp { get; set; }
    public float Value { get; set; }
}