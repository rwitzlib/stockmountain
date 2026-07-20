using System.Diagnostics.CodeAnalysis;

namespace Massive.Client.Requests;

[ExcludeFromCodeCoverage]
public class MassiveAggregateRequest
{
    public string? Ticker { get; set; }
    public int Multiplier { get; set; }
    public string? Timespan { get; set; }
    public string? From { get; set; }
    public string? To { get; set; }
    public bool Adjusted { get; set; } = true;
    public string Sort { get; set; } = "asc";
    public int Limit { get; set; } = 5000;
}
