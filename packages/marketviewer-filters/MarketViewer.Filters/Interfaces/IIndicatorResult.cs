using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Filters.Interfaces;

/// <summary>
/// Represents the result of an indicator calculation at a specific point in time
/// </summary>
public interface IIndicatorResult
{
    /// <summary>
    /// The timestamp for this result
    /// </summary>
    long Timestamp { get; }

    /// <summary>
    /// Gets the value for the specified field
    /// </summary>
    /// <param name="fieldName">The field name (e.g., "value", "signal", "histogram")</param>
    /// <returns>The field value</returns>
    double GetFieldValue(string fieldName = "value");

    /// <summary>
    /// Gets all available field names for this indicator result
    /// </summary>
    IEnumerable<string> GetAvailableFields();
}

/// <summary>
/// Base implementation of IIndicatorResult
/// </summary>
[ExcludeFromCodeCoverage]
public abstract class BaseIndicatorResult : IIndicatorResult
{
    public long Timestamp { get; set; }

    public abstract double GetFieldValue(string fieldName = "value");

    public abstract IEnumerable<string> GetAvailableFields();
}

/// <summary>
/// Simple indicator result with just a value field
/// </summary>
[ExcludeFromCodeCoverage]
public class SimpleIndicatorResult : BaseIndicatorResult
{
    public double Value { get; set; }

    public override double GetFieldValue(string fieldName = "value")
    {
        return fieldName.ToLowerInvariant() switch
        {
            "value" => Value,
            _ => throw new ArgumentException($"Field '{fieldName}' not available. Available fields: {string.Join(", ", GetAvailableFields())}")
        };
    }

    public override IEnumerable<string> GetAvailableFields()
    {
        return ["value"];
    }
}

/// <summary>
/// MACD indicator result with multiple fields
/// </summary>
[ExcludeFromCodeCoverage]
public class MacdResult : BaseIndicatorResult
{
    public double Value { get; set; }      // MACD line
    public double Signal { get; set; }     // Signal line
    public double Histogram { get; set; }  // Histogram (MACD - Signal)
    // Internal state for incremental updates
    public double FastMA { get; set; }
    public double SlowMA { get; set; }
    public double SignalMA { get; set; }

    public override double GetFieldValue(string fieldName = "value")
    {
        return fieldName.ToLowerInvariant() switch
        {
            "value" or "macd" => Value,
            "signal" => Signal,
            "histogram" => Histogram,
            _ => throw new ArgumentException($"Field '{fieldName}' not available. Available fields: {string.Join(", ", GetAvailableFields())}")
        };
    }

    public override IEnumerable<string> GetAvailableFields()
    {
        return ["value", "macd", "signal", "histogram"];
    }
}

/// <summary>
/// RSI indicator result with value and threshold fields
/// </summary>
[ExcludeFromCodeCoverage]
public class RsiResult : BaseIndicatorResult
{
    public double Value { get; set; }      // RSI line
    public double Overbought { get; set; }     // Overbought line
    public double Oversold { get; set; }  // Oversold line
    // Internal state to support incremental RSI updates for EMA/Wilder smoothing
    public double AvgGain { get; set; }
    public double AvgLoss { get; set; }

    public override double GetFieldValue(string fieldName = "value")
    {
        return fieldName.ToLowerInvariant() switch
        {
            "value" => Value,
            "overbought" => Overbought,
            "oversold" => Oversold,
            _ => throw new ArgumentException($"Field '{fieldName}' not available. Available fields: {string.Join(", ", GetAvailableFields())}")
        };
    }

    public override IEnumerable<string> GetAvailableFields()
    {
        return ["value", "overbought", "oversold"];
    }
}

/// <summary>
/// Support/resistance zone result exposing zone levels, strength, and proximity metadata.
/// </summary>
[ExcludeFromCodeCoverage]
public class SupportResistanceResult : BaseIndicatorResult
{
    public double Value { get; set; }
    public double Support { get; set; }
    public double Resistance { get; set; }
    public double SupportStrength { get; set; }
    public double ResistanceStrength { get; set; }
    public double SupportZoneWidth { get; set; }
    public double ResistanceZoneWidth { get; set; }
    public double SupportDistance { get; set; }
    public double ResistanceDistance { get; set; }
    public double SupportDistancePercent { get; set; }
    public double ResistanceDistancePercent { get; set; }
    public double SupportTouches { get; set; }
    public double ResistanceTouches { get; set; }
    public double SupportUpper { get; set; }
    public double SupportLower { get; set; }
    public double ResistanceUpper { get; set; }
    public double ResistanceLower { get; set; }
    public double NearSupport { get; set; }
    public double NearResistance { get; set; }

    public override double GetFieldValue(string fieldName = "value")
    {
        return fieldName.ToLowerInvariant() switch
        {
            "value" => Value,
            "support" => Support,
            "resistance" => Resistance,
            "support_strength" => SupportStrength,
            "resistance_strength" => ResistanceStrength,
            "support_zone_width" => SupportZoneWidth,
            "resistance_zone_width" => ResistanceZoneWidth,
            "support_distance" => SupportDistance,
            "resistance_distance" => ResistanceDistance,
            "support_distance_pct" or "support_distance_percent" => SupportDistancePercent,
            "resistance_distance_pct" or "resistance_distance_percent" => ResistanceDistancePercent,
            "support_touches" => SupportTouches,
            "resistance_touches" => ResistanceTouches,
            "support_upper" => SupportUpper,
            "support_lower" => SupportLower,
            "resistance_upper" => ResistanceUpper,
            "resistance_lower" => ResistanceLower,
            "near_support" => NearSupport,
            "near_resistance" => NearResistance,
            _ => throw new ArgumentException($"Field '{fieldName}' not available. Available fields: {string.Join(", ", GetAvailableFields())}")
        };
    }

    public override IEnumerable<string> GetAvailableFields()
    {
        return
        [
            "value",
            "support",
            "resistance",
            "support_strength",
            "resistance_strength",
            "support_zone_width",
            "resistance_zone_width",
            "support_distance",
            "resistance_distance",
            "support_distance_pct",
            "resistance_distance_pct",
            "support_touches",
            "resistance_touches",
            "support_upper",
            "support_lower",
            "resistance_upper",
            "resistance_lower",
            "near_support",
            "near_resistance"
        ];
    }
}
