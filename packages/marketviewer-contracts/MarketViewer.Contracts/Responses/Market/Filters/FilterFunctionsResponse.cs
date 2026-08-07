using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Responses.Market.Filters;

[ExcludeFromCodeCoverage]
public class FilterFunctionsResponse
{
    public required List<FilterFunctionInfo> Functions { get; init; }
}

/// <summary>
/// Autocomplete metadata for one completable token: an indicator function, a price
/// literal, or a comparison/logical operator.
/// </summary>
[ExcludeFromCodeCoverage]
public class FilterFunctionInfo
{
    /// <summary>function | literal | operator</summary>
    public required string Kind { get; init; }
    public required string Name { get; init; }

    /// <summary>Display signature, e.g. "rsi(period[, overbought, oversold, type])".</summary>
    public required string Signature { get; init; }

    /// <summary>Text inserted on selection, e.g. "rsi(14)". The UI selects the first argument.</summary>
    public required string Snippet { get; init; }

    public required string Description { get; init; }

    /// <summary>Ordered parameter names for active-argument highlighting; "?" suffix marks optional, e.g. ["series", "period?"].</summary>
    public List<string>? Params { get; init; }

    /// <summary>Dot-accessible fields, e.g. macd → ["value", "signal", "histogram"].</summary>
    public List<string>? Fields { get; init; }
}
