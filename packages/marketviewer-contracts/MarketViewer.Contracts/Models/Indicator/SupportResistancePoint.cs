using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models.Indicator;

[ExcludeFromCodeCoverage]
public class SupportResistancePoint : IndicatorPoint
{
    public float Support { get; set; }
    public float Resistance { get; set; }
    public float SupportStrength { get; set; }
    public float ResistanceStrength { get; set; }
    public float SupportZoneWidth { get; set; }
    public float ResistanceZoneWidth { get; set; }
    public float SupportDistance { get; set; }
    public float ResistanceDistance { get; set; }
    public float SupportDistancePercent { get; set; }
    public float ResistanceDistancePercent { get; set; }
    public float SupportTouches { get; set; }
    public float ResistanceTouches { get; set; }
    public float SupportUpper { get; set; }
    public float SupportLower { get; set; }
    public float ResistanceUpper { get; set; }
    public float ResistanceLower { get; set; }
    public float NearSupport { get; set; }
    public float NearResistance { get; set; }
}
