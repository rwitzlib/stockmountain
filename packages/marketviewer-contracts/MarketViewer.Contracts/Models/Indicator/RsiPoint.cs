using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models.Indicator;

[ExcludeFromCodeCoverage]
public class RsiPoint : IndicatorPoint
{
    public float Upper { get; set; }
    public float Lower { get; set; }
}
