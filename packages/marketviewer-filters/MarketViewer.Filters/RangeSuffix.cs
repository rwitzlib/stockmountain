using System.Globalization;
using System.Text.RegularExpressions;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;

namespace MarketViewer.Filters;

/// <summary>
/// The single definition of the <c>[timeframe, candles, mode]</c> line suffix: slot names, the
/// tokens each slot accepts, and the canonical spelling of a timeframe. Shared by the parser
/// (strict positional parsing), the canonical printer and the <c>/filters/functions</c> catalog
/// pseudo-entry the composer uses for the bracket hint, so there is one place to extend when the
/// grammar grows.
/// </summary>
public static class RangeSuffix
{
    /// <summary>Catalog name of the pseudo-entry; not a DSL token.</summary>
    public const string CatalogName = "range";

    public const string Signature = "[timeframe, candles, mode]";
    public const string Snippet = "[1m]";
    public const string Description =
        "Line suffix: which candles the comparisons look at. Timeframe is required; candles (default 1) and mode (all | any, default all) are optional.";

    public static readonly IReadOnlyList<string> SlotNames = ["timeframe", "candles?", "mode?"];

    /// <summary>Timeframe tokens offered by autocomplete. Any quantity + unit parses; these are the common ones.</summary>
    public static readonly IReadOnlyList<string> TimeframeOptions = ["1m", "5m", "15m", "30m", "1h", "1d", "1w"];

    public static readonly IReadOnlyList<string> ModeOptions = ["all", "any"];

    public static Timeframe DefaultTimeframe => new(1, Timespan.minute);

    public static bool TryParseMode(string value, out RangeEvaluationMode mode)
    {
        if (value.Equals("any", StringComparison.OrdinalIgnoreCase))
        {
            mode = RangeEvaluationMode.Any;
            return true;
        }

        if (value.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            mode = RangeEvaluationMode.All;
            return true;
        }

        mode = default;
        return false;
    }

    public static string FormatMode(RangeEvaluationMode mode) => mode == RangeEvaluationMode.Any ? "any" : "all";

    /// <summary>
    /// Parses a timeframe token: a quantity plus a unit (<c>5m</c>, <c>1h</c>, <c>2d</c>) or a bare
    /// unit meaning one of it (<c>h</c> = <c>1h</c>). Units accept the common spellings
    /// (m/min/minute, h/hr/hour, d/day, w/wk/week, mo/month).
    /// </summary>
    public static bool TryParseTimeframe(string token, out Timeframe timeframe)
    {
        timeframe = null!;
        var match = Regex.Match(token, @"^(?<qty>\d+)\s*(?<unit>[a-zA-Z]+)$");
        if (match.Success)
        {
            if (!int.TryParse(match.Groups["qty"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var qty) || qty < 1)
            {
                return false;
            }

            if (Enum.TryParse<Timespan>(NormalizeUnit(match.Groups["unit"].Value), true, out var unitSpan))
            {
                timeframe = new Timeframe(qty, unitSpan);
                return true;
            }

            return false;
        }

        if (Regex.IsMatch(token, @"^[a-zA-Z]+$") && Enum.TryParse<Timespan>(NormalizeUnit(token), true, out var bareSpan))
        {
            timeframe = new Timeframe(1, bareSpan);
            return true;
        }

        return false;
    }

    /// <summary>Canonical short form: <c>{multiplier}{unit}</c>: 1m, 5m, 1h, 1d, 1w, 1mo, 1q, 1y.</summary>
    public static string FormatTimeframe(Timeframe timeframe)
    {
        var unit = timeframe.Timespan switch
        {
            Timespan.minute => "m",
            Timespan.hour => "h",
            Timespan.day => "d",
            Timespan.week => "w",
            Timespan.month => "mo",
            Timespan.quarter => "q",
            Timespan.year => "y",
            _ => timeframe.Timespan.ToString(),
        };
        return $"{timeframe.Multiplier}{unit}";
    }

    private static string NormalizeUnit(string unit)
    {
        var token = unit.Trim().ToLowerInvariant();
        return token switch
        {
            "m" or "min" or "mins" or "minute" or "minutes" => nameof(Timespan.minute),
            "h" or "hr" or "hrs" or "hour" or "hours" => nameof(Timespan.hour),
            "d" or "day" or "days" => nameof(Timespan.day),
            "w" or "wk" or "wks" or "week" or "weeks" => nameof(Timespan.week),
            "mo" or "mon" or "month" or "months" => nameof(Timespan.month),
            "q" or "qtr" or "quarter" or "quarters" => nameof(Timespan.quarter),
            "y" or "yr" or "year" or "years" => nameof(Timespan.year),
            _ => unit
        };
    }
}
