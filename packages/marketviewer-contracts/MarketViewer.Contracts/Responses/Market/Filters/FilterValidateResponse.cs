using MarketViewer.Contracts.Models;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Responses.Market.Filters;

[ExcludeFromCodeCoverage]
public class FilterValidateResponse
{
    public required List<FilterValidationResult> Results { get; init; }
}

[ExcludeFromCodeCoverage]
public class FilterValidationResult
{
    /// <summary>The expression as submitted.</summary>
    public required string Expression { get; init; }
    public bool Valid { get; init; }
    public string? Error { get; init; }

    /// <summary>Human-readable phrasing of the expression, e.g. "rsi(14) is below 30 on all of the last 5 1m candles".</summary>
    public string? Description { get; init; }

    /// <summary>
    /// The canonical spelling of the expression: explicit timeframe on series lines, explicit mode
    /// when more than one candle is examined, normalized spacing. This is what gets stored and what
    /// <see cref="Segments"/> index into. Clients replace the user's text with it on commit.
    /// </summary>
    public string? Canonical { get; init; }

    /// <summary>The line's timeframe (1m when not written); null for scalar-only lines such as "float &lt; 20000000".</summary>
    public Timeframe? Timeframe { get; init; }

    /// <summary>
    /// Display pieces of <see cref="Canonical"/> in order, each a character span with a rendering role.
    /// Text between segments is punctuation. A quick edit is a text splice on one segment's span followed
    /// by re-validation; clients never rebuild the expression themselves.
    /// </summary>
    public List<FilterSegment>? Segments { get; init; }
}

/// <summary>One span of a canonical filter expression.</summary>
[ExcludeFromCodeCoverage]
public class FilterSegment
{
    /// <summary>function | data | literal | op | logic | timeframe</summary>
    public required string Role { get; init; }

    /// <summary>Inclusive start offset into the canonical text.</summary>
    public required int Start { get; init; }

    /// <summary>Exclusive end offset into the canonical text.</summary>
    public required int End { get; init; }

    /// <summary>What a quick edit of this span changes: value | op | timeframe | candles | mode. Null when not editable.</summary>
    public string? Edit { get; init; }
}
