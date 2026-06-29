using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models.Indicator;

[ExcludeFromCodeCoverage]
public class MacdPoint : IndicatorPoint
{
    public float Histogram { get; set; }
    public float Signal { get; set; }
}
