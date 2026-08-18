using MarketViewer.Filters.Registry;

namespace MarketViewer.Filters.Expressions;

/// <summary>
/// Cost/selectivity lookup for the planner. Values come from each function's
/// <see cref="FilterFunctionAttribute"/> (and <see cref="KeywordRegistry"/> for keywords) — there
/// is no separate table to keep in sync.
/// </summary>
public static class FunctionHeuristicsRegistry
{
    /// <summary>
    /// cost: relative compute cost (lower is cheaper);
    /// selectivity: probability that the function contributes to a TRUE outcome (0..1).
    /// Unknown names fall back to a neutral (2, 0.5).
    /// </summary>
    public static (double cost, double selectivity) GetHeuristics(string functionName)
    {
        if (FunctionRegistry.TryGet(functionName, out var descriptor))
        {
            return (descriptor.Cost, descriptor.Selectivity);
        }
        return (2, 0.5); // default neutral
    }
}
