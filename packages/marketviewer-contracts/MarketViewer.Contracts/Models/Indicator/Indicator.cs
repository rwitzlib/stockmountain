using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using MarketViewer.Contracts.Converters;

namespace MarketViewer.Contracts.Models.Indicator;

[ExcludeFromCodeCoverage]
[JsonConverter(typeof(IndicatorConverter))]
public class Indicator
{
    /// <summary>
    /// Filter-DSL function name (e.g. "sma", "macd", alias "sr"). Resolved against the
    /// MarketViewer.Filters FunctionRegistry with the Chart context; unknown or non-chartable
    /// names are rejected by IndicatorCalculationService.
    /// </summary>
    public string Type { get; set; }
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