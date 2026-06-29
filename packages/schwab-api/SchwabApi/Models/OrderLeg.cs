using System.Diagnostics.CodeAnalysis;

namespace SchwabApi.Models;

[ExcludeFromCodeCoverage]
public class OrderLeg
{
    public string OrderLegType { get; set; }
    public int LegId { get; set; }
    public Instrument Instrument { get; set; }
    public string Instruction { get; set; }
    public string PositionEffect { get; set; }
    public float Quantity { get; set; }
    public string QuantityType { get; set; }
    public string DivCapGains { get; set; }
    public string ToSymbol { get; set; }
}
