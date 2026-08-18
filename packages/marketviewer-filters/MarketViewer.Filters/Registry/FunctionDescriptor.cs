using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Registry;

/// <summary>
/// Immutable, resolved metadata for one DSL token (function or keyword). Built once from
/// <see cref="FilterFunctionAttribute"/>s (functions) and <see cref="KeywordRegistry"/> (keywords).
/// </summary>
public sealed record FunctionDescriptor
{
    public required string Name { get; init; }
    public IReadOnlyList<string> Aliases { get; init; } = [];
    public required FunctionKind Kind { get; init; }
    public required string Signature { get; init; }
    public required string Snippet { get; init; }
    public required string Description { get; init; }
    public IReadOnlyList<string> Params { get; init; } = [];
    public IReadOnlyList<string> Fields { get; init; } = [];
    public double Cost { get; init; } = 2;
    public double Selectivity { get; init; } = 0.5;
    public FilterContext Contexts { get; init; } = FilterContext.Filters;

    /// <summary>Keyword only: true when the value is a per-ticker scalar (e.g. float) rather than a per-bar series.</summary>
    public bool IsScalar { get; init; }

    /// <summary>Implementing class for functions; null for keywords.</summary>
    public Type? ImplementationType { get; init; }

    public bool IsKeyword => Kind == FunctionKind.Keyword;
    public bool IsIncremental => ImplementationType is not null && typeof(IIncrementalSeriesFunction).IsAssignableFrom(ImplementationType);
    public bool IsSeriesFunction => ImplementationType is not null && typeof(ISeriesFunction).IsAssignableFrom(ImplementationType);
    public bool IsBooleanFunction => ImplementationType is not null && typeof(IBooleanFunction).IsAssignableFrom(ImplementationType);

    /// <summary>All names the parser accepts for this token (name first, then aliases).</summary>
    public IEnumerable<string> AllNames => new[] { Name }.Concat(Aliases);

    public bool SupportsContext(FilterContext context) => (Contexts & context) == context;

    /// <summary>Instantiates the implementation (functions must have a public parameterless constructor).</summary>
    public IFunction CreateFunction()
    {
        if (ImplementationType is null)
            throw new InvalidOperationException($"'{Name}' is a keyword and has no function implementation.");
        return (IFunction)(Activator.CreateInstance(ImplementationType)
            ?? throw new InvalidOperationException($"Could not instantiate {ImplementationType.FullName}"));
    }
}
