namespace MarketViewer.Filters.Expressions;

public static class FunctionHeuristicsRegistry
{
    // cost: relative compute cost (lower is cheaper)
    // selectivity: probability that the function contributes to a TRUE outcome (0..1)
    private static readonly Dictionary<string, (double cost, double selectivity)> Heuristics =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Data/volume based
            ["adv"] = (1, 0.4),

            // Moving averages
            ["sma"] = (2, 0.5),
            ["ema"] = (2, 0.5),

            // Cross detection tends to be rarer and a bit more expensive
            ["crosses_over"] = (3, 0.2),
            ["crosses_under"] = (3, 0.2),

            // Multi-series heavy indicators
            ["macd"] = (4, 0.3),
            ["rsi"] = (4, 0.3),

            // Transforms
            ["slope"] = (1.5, 0.5),
        };

    public static (double cost, double selectivity) GetHeuristics(string functionName)
    {
        if (Heuristics.TryGetValue(functionName, out var h))
        {
            return h;
        }
        return (2, 0.5); // default neutral
    }
}

