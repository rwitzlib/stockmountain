namespace MarketViewer.Filters.Registry;

/// <summary>
/// Bare data keywords (no parentheses). These are not classes, so they are declared here once
/// and consumed by the parser (<c>IsDataAccessKeyword</c>), <c>DataAccessExpression.IsScalar</c>,
/// the planner and the <c>/filters/functions</c> catalog. Adding a keyword also requires a case in
/// <c>DataAccessExpression</c>, a docs page, unit tests and a golden outcome case.
/// </summary>
public static class KeywordRegistry
{
    public static IReadOnlyList<FunctionDescriptor> All { get; } =
    [
        Price("close", "Closing price series"),
        Price("open", "Opening price series"),
        Price("high", "High price series"),
        Price("low", "Low price series"),
        new()
        {
            Name = "volume", Kind = FunctionKind.Keyword, Signature = "volume", Snippet = "volume",
            Description = "Volume series (per bar; the forming bar's volume so far)",
            Cost = 1, Contexts = FilterContext.Filters,
        },
        new()
        {
            Name = "float", Kind = FunctionKind.Keyword, Signature = "float", Snippet = "float",
            Description = "Ticker share float (scalar; fails comparison when unavailable)",
            Cost = 0.01, Contexts = FilterContext.Filters, IsScalar = true,
        },
        new()
        {
            Name = "time", Kind = FunctionKind.Keyword, Signature = "time", Snippet = "time",
            Description = "Evaluation time of day, Eastern. Compare against HH:MM, or use .hour / .minute",
            Fields = ["hour", "minute"], Cost = 0.01, Contexts = FilterContext.Filters,
        },
    ];

    private static readonly Dictionary<string, FunctionDescriptor> ByName =
        All.ToDictionary(k => k.Name, StringComparer.OrdinalIgnoreCase);

    public static bool IsKeyword(string token) => ByName.ContainsKey(token);

    public static bool TryGet(string token, out FunctionDescriptor descriptor) => ByName.TryGetValue(token, out descriptor!);

    private static FunctionDescriptor Price(string name, string description) => new()
    {
        Name = name, Kind = FunctionKind.Keyword, Signature = name, Snippet = name,
        Description = description, Cost = 1, Contexts = FilterContext.Filters,
    };
}
