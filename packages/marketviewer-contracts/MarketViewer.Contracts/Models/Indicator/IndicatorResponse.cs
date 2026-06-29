using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models.Indicator;

[ExcludeFromCodeCoverage]
public class IndicatorResponse
{
    public string Name { get; set; }
    public List<IndicatorPoint> Results { get; set; }
}