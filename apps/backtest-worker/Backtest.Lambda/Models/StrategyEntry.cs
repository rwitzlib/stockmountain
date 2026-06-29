using Polygon.Client.Models;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Backtest.Lambda.Models;

[ExcludeFromCodeCoverage]
public class StrategyEntry
{
    [JsonPropertyName("t")]
    public string Ticker { get; set; }
    [JsonPropertyName("s")]
    public DateTimeOffset Start { get; set; }
    public IEnumerable<Bar> Bars { get; set; }
}
