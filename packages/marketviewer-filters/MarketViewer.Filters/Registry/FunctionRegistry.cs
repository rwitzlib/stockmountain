using System.Reflection;
using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Registry;

/// <summary>
/// Reflects once over the MarketViewer.Filters assembly for every <c>IFunction</c> class carrying
/// <see cref="FilterFunctionAttribute"/> and exposes the resolved descriptors. Everything that used
/// to be a hand-maintained table (parser function map, autocomplete catalog, cost heuristics,
/// chartable indicator list) reads from here.
/// </summary>
public static class FunctionRegistry
{
    private static readonly Lazy<Snapshot> Instance = new(Build, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Function descriptors only (no keywords), ordered by name.</summary>
    public static IReadOnlyList<FunctionDescriptor> Functions => Instance.Value.Functions;

    /// <summary>Functions and keywords, functions first — the shape the autocomplete catalog wants.</summary>
    public static IReadOnlyList<FunctionDescriptor> All => Instance.Value.All;

    /// <summary>Resolves a function name or alias (case-insensitive). Keywords are not included; see <see cref="KeywordRegistry"/>.</summary>
    public static bool TryGetFunction(string nameOrAlias, out FunctionDescriptor descriptor)
        => Instance.Value.ByName.TryGetValue(nameOrAlias, out descriptor!);

    /// <summary>Resolves a function name/alias or a keyword.</summary>
    public static bool TryGet(string token, out FunctionDescriptor descriptor)
        => TryGetFunction(token, out descriptor) || KeywordRegistry.TryGet(token, out descriptor);

    /// <summary>
    /// Fresh function instances keyed by every accepted name (aliases share one instance),
    /// for the parser. Instances are stateless today but callers get their own set anyway.
    /// </summary>
    public static Dictionary<string, IFunction> CreateFunctionMap()
    {
        var map = new Dictionary<string, IFunction>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in Functions)
        {
            var instance = d.CreateFunction();
            foreach (var name in d.AllNames) map[name] = instance;
        }
        return map;
    }

    private static Snapshot Build()
    {
        var descriptors = new List<FunctionDescriptor>();
        foreach (var type in typeof(FunctionRegistry).Assembly.GetTypes())
        {
            var attribute = type.GetCustomAttribute<FilterFunctionAttribute>(inherit: false);
            if (attribute is null) continue;

            if (!typeof(IFunction).IsAssignableFrom(type) || type.IsAbstract)
                throw new InvalidOperationException($"[FilterFunction(\"{attribute.Name}\")] on {type.FullName}: must be a concrete IFunction.");
            if (attribute.Kind == FunctionKind.Keyword)
                throw new InvalidOperationException($"[FilterFunction(\"{attribute.Name}\")] on {type.FullName}: keywords are declared in KeywordRegistry, not with the attribute.");
            if (type.GetConstructor(Type.EmptyTypes) is null)
                throw new InvalidOperationException($"[FilterFunction(\"{attribute.Name}\")] on {type.FullName}: needs a public parameterless constructor.");

            descriptors.Add(new FunctionDescriptor
            {
                Name = attribute.Name,
                Aliases = attribute.Aliases,
                Kind = attribute.Kind,
                Signature = attribute.Signature,
                Snippet = attribute.Snippet,
                Description = attribute.Description,
                Params = attribute.Params,
                Fields = attribute.Fields,
                Cost = attribute.Cost,
                Selectivity = attribute.Selectivity,
                Contexts = attribute.Contexts,
                ImplementationType = type,
            });
        }

        descriptors.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

        var byName = new Dictionary<string, FunctionDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in descriptors)
        {
            foreach (var name in d.AllNames)
            {
                if (KeywordRegistry.IsKeyword(name))
                    throw new InvalidOperationException($"Function name '{name}' collides with a keyword.");
                if (!byName.TryAdd(name, d))
                    throw new InvalidOperationException($"Duplicate filter function name '{name}' ({d.ImplementationType!.Name} and {byName[name].ImplementationType!.Name}).");
            }
        }

        return new Snapshot(descriptors, [.. descriptors, .. KeywordRegistry.All], byName);
    }

    private sealed record Snapshot(
        IReadOnlyList<FunctionDescriptor> Functions,
        IReadOnlyList<FunctionDescriptor> All,
        Dictionary<string, FunctionDescriptor> ByName);
}
