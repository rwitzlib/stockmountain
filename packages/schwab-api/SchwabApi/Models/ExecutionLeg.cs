using SchwabApi.Converters;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace SchwabApi.Models;

[ExcludeFromCodeCoverage]
public class ExecutionLeg
{
    public int LegId { get; set; }
    public float Price { get; set; }
    public float Quantity { get; set; }
    public float MismarkedQuantity { get; set; }
    public int InstrumentId { get; set; }

    [JsonConverter(typeof(DateTimeOffsetConverter))]
    public DateTimeOffset Time { get; set; }
}
