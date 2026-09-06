using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Responses.Market.Filters;

[ExcludeFromCodeCoverage]
public class FilterFunctionsResponse
{
    public required List<FilterFunctionInfo> Functions { get; init; }
}

/// <summary>
/// Autocomplete metadata for one completable token: an indicator function, a price
/// literal, or the "[timeframe, candles, mode]" line suffix (kind "suffix").
/// </summary>
[ExcludeFromCodeCoverage]
public class FilterFunctionInfo
{
    /// <summary>function | literal | suffix. Functions additionally carry <see cref="FunctionKind"/>.</summary>
    public required string Kind { get; init; }

    /// <summary>series | transform | boolean | keyword — the registry kind (see FilterFunctionAttribute).</summary>
    public string? FunctionKind { get; init; }
    public required string Name { get; init; }

    /// <summary>Display signature, e.g. "rsi(period[, overbought, oversold, type])".</summary>
    public required string Signature { get; init; }

    /// <summary>Text inserted on selection, e.g. "rsi(14)". The UI selects the first argument.</summary>
    public required string Snippet { get; init; }

    public required string Description { get; init; }

    /// <summary>Ordered parameter names for active-argument highlighting; "?" suffix marks optional, e.g. ["series", "period?"].</summary>
    public List<string>? Params { get; init; }

    /// <summary>
    /// Fixed choices for parameters that take one of a few tokens, keyed by parameter name
    /// (without the "?" suffix), e.g. the suffix's timeframe → ["1m", "5m", …], mode → ["all", "any"].
    /// </summary>
    public Dictionary<string, List<string>>? ParamOptions { get; init; }

    /// <summary>Dot-accessible fields, e.g. macd → ["value", "signal", "histogram"].</summary>
    public List<string>? Fields { get; init; }

    /// <summary>Alternative names accepted by the parser, e.g. support_resistance → ["sr"].</summary>
    public List<string>? Aliases { get; init; }

    /// <summary>Contexts the token is valid in: any of "scan", "backtest", "chart".</summary>
    public List<string>? Contexts { get; init; }

    /// <summary>Relative path of the user docs page, e.g. "/docs/filters/rsi".</summary>
    public string? DocsUrl { get; init; }
}
