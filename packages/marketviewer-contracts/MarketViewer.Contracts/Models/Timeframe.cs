using MarketViewer.Contracts.Enums;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models;

[ExcludeFromCodeCoverage]
public class Timeframe(int multiplier, Timespan timespan)
{
    public int Multiplier { get; set; } = multiplier;
    public Timespan Timespan { get; set; } = timespan;
}
