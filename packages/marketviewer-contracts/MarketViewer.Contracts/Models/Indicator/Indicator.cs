using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using MarketViewer.Contracts.Converters;
using MarketViewer.Contracts.Enums;

namespace MarketViewer.Contracts.Models.Indicator;

[ExcludeFromCodeCoverage]
[JsonConverter(typeof(IndicatorConverter))]
public class Indicator
{
    public StudyType Type { get; set; }
    public string[] Parameters { get; set; }
    public string Selector { get; set; }

    public override string ToString()
    {
        var paramsString = Parameters != null && Parameters.Length > 0
            ? $"({string.Join(" ", Parameters)})"
            : string.Empty;

        if (string.IsNullOrWhiteSpace(Selector))
        {
            return $"{Type}{paramsString}";
        }
        else
        {
            return $"{Type}{paramsString}.{Selector}";
        }
    }
}