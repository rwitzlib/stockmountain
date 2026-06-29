using System;
using MarketViewer.Filters;

namespace MarketViewer.Filters.Operators.Comparison;

internal static class RangeEvaluationHelper
{
    public static bool Evaluate(int count, RangeEvaluationMode mode, Func<int, bool> predicate)
    {
        if (count <= 0)
        {
            return false;
        }

        if (mode == RangeEvaluationMode.All)
        {
            for (int i = 0; i < count; i++)
            {
                if (!predicate(i))
                {
                    return false;
                }
            }

            return true;
        }

        for (int i = 0; i < count; i++)
        {
            if (predicate(i))
            {
                return true;
            }
        }

        return false;
    }
}
