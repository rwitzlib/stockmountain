using System;
using MarketViewer.Filters;

namespace MarketViewer.Filters.Operators.Comparison;

internal static class RangeEvaluationHelper
{
    /// <summary>
    /// Comparison operands can arrive as <c>List&lt;IIndicatorResult&gt;</c> (data access, indicator
    /// functions) or <c>List&lt;double&gt;</c> (dot-field access, transforms). When one side is each,
    /// project the indicator side to its "value" field so both are <c>List&lt;double&gt;</c>;
    /// otherwise the operators fall through to a scalar cast and throw. Golden case:
    /// "close &gt; support_resistance().support", "close &gt; macd(12,26,9,ema).signal".
    /// </summary>
    public static void NormalizeMixedSeries(ref object? left, ref object right)
    {
        if (left is List<Interfaces.IIndicatorResult> leftSeries && right is List<double>)
        {
            left = leftSeries.Select(r => r.GetFieldValue("value")).ToList();
        }
        else if (right is List<Interfaces.IIndicatorResult> rightSeries && left is List<double>)
        {
            right = rightSeries.Select(r => r.GetFieldValue("value")).ToList();
        }
    }

    /// <summary>
    /// Aggregates a comparison over the last <paramref name="count"/> aligned values of a window of
    /// <paramref name="range"/> candles. <c>all</c> requires the full window: fewer than
    /// <paramref name="range"/> values available is false (plan 20, decision 4). <c>any</c> is true
    /// as soon as one available value satisfies the predicate.
    /// </summary>
    public static bool Evaluate(int count, int range, RangeEvaluationMode mode, Func<int, bool> predicate)
    {
        if (count <= 0)
        {
            return false;
        }

        if (mode == RangeEvaluationMode.All)
        {
            if (count < range)
            {
                return false;
            }

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
