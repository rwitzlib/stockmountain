using System.Diagnostics.CodeAnalysis;

namespace SchwabApi.Models;

[ExcludeFromCodeCoverage]
public class Instrument
{
    public string Cusip { get; set; }
    public string Symbol { get; set; }
    public string Description { get; set; }
    public int InstrumentId { get; set; }
    public float NetChange { get; set; }
    public string AssetType { get; set; }
}
