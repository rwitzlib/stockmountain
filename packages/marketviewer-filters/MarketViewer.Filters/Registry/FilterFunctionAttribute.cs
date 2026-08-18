namespace MarketViewer.Filters.Registry;

/// <summary>
/// Where a filter function may be used. Bit flags — a function is normally valid in every
/// filter context (<see cref="Scan"/> | <see cref="Backtest"/>); <see cref="Chart"/> marks
/// series indicators that can also be plotted via <c>/stocks</c>.
/// </summary>
[Flags]
public enum FilterContext
{
    None = 0,
    /// <summary>Live scanner and <c>POST /scan</c>.</summary>
    Scan = 1,
    /// <summary>Backtest entry filters (Backtest.Lambda ScannerService).</summary>
    Backtest = 2,
    /// <summary>Chartable indicator on <c>POST /stocks</c>.</summary>
    Chart = 4,
    Filters = Scan | Backtest,
    All = Scan | Backtest | Chart,
}

/// <summary>What shape of value the token produces. Drives the testing bar (see plan 15).</summary>
public enum FunctionKind
{
    /// <summary>Numeric series indicator (sma, rsi, vwap …). Must implement <c>IIncrementalSeriesFunction</c>.</summary>
    Series,
    /// <summary>Series-in / series-out transform (slope …).</summary>
    Transform,
    /// <summary>Boolean function (crosses_over …).</summary>
    Boolean,
    /// <summary>Bare data keyword (close, volume, float, time …). Not a class — see <see cref="KeywordRegistry"/>.</summary>
    Keyword,
}

/// <summary>
/// The single source of truth for a filter function's metadata. Put this on every
/// <c>IFunction</c> implementation; the parser table, <c>/filters/functions</c> catalog,
/// cost heuristics and context enforcement are all derived from it by reflection
/// (<see cref="FunctionRegistry"/>). Forgetting to register a function is therefore impossible;
/// forgetting docs/tests is caught by <c>RegistryParityTests</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class FilterFunctionAttribute(string name) : Attribute
{
    /// <summary>DSL name, lower-case, e.g. "rsi".</summary>
    public string Name { get; } = name;

    /// <summary>Alternative DSL names that resolve to the same implementation, e.g. "sr".</summary>
    public string[] Aliases { get; init; } = [];

    public FunctionKind Kind { get; init; } = FunctionKind.Series;

    /// <summary>Display signature for autocomplete, e.g. "rsi(period, overbought, oversold, type)".</summary>
    public string Signature { get; init; } = "";

    /// <summary>Text inserted on autocomplete selection, e.g. "rsi(14,70,30,wilders)". Must parse.</summary>
    public string Snippet { get; init; } = "";

    /// <summary>One-line description for autocomplete. Long-form docs live in docs/filters/{name}.md.</summary>
    public string Description { get; init; } = "";

    /// <summary>Ordered parameter names; "?" suffix marks optional, e.g. ["series", "period?"].</summary>
    public string[] Params { get; init; } = [];

    /// <summary>Dot-accessible result fields, e.g. ["value", "signal", "histogram"].</summary>
    public string[] Fields { get; init; } = [];

    /// <summary>Relative compute cost (lower is cheaper); used to order filters. Default 2.</summary>
    public double Cost { get; init; } = 2;

    /// <summary>Probability the function contributes to a TRUE outcome (0..1]; used to order filters. Default 0.5.</summary>
    public double Selectivity { get; init; } = 0.5;

    public FilterContext Contexts { get; init; } = FilterContext.Filters;
}
