using System;
using MarketViewer.Filters;
using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Operators.Comparison;

/// <summary>
/// Equal operator (= or ==)
/// </summary>
public class EqualOperator : IComparisonOperator
{
    public string Symbol => "=";
    public int Precedence => 7;
    public bool IsBinary => true;
    public bool IsUnary => false;
    private const double Epsilon = 1e-9;

    public object Execute(object? left, object right, ExpressionContext context)
    {
        if (left == null)
        {
            return right == null;
        }

        var range = context.CandleRange ?? 1; // Default range if not specified
        var mode = context.RangeEvaluationMode;

        if (left is List<IIndicatorResult> leftSeries && right is List<IIndicatorResult> rightSeries)
        {
            return CompareSeries(leftSeries, rightSeries, range, mode);
        }

        if (left is List<double> leftDoubles && right is List<double> rightDoubles)
        {
            return CompareSeries(leftDoubles, rightDoubles, range, mode);
        }

        if (left is List<IIndicatorResult> leftSeriesScalar)
        {
            var rightScalar = Convert.ToDouble(right);
            return CompareSeriesScalar(leftSeriesScalar, range, mode, value => Math.Abs(value - rightScalar) <= Epsilon);
        }

        if (left is List<double> leftDoubleSeriesScalar)
        {
            var rightScalar = Convert.ToDouble(right);
            return CompareSeriesScalar(leftDoubleSeriesScalar, range, mode, value => Math.Abs(value - rightScalar) <= Epsilon);
        }

        if (right is List<IIndicatorResult> rightSeriesScalar)
        {
            var leftScalar = Convert.ToDouble(left);
            return CompareSeriesScalar(rightSeriesScalar, range, mode, value => Math.Abs(leftScalar - value) <= Epsilon);
        }

        if (right is List<double> rightDoubleSeriesScalar)
        {
            var leftScalar = Convert.ToDouble(left);
            return CompareSeriesScalar(rightDoubleSeriesScalar, range, mode, value => Math.Abs(leftScalar - value) <= Epsilon);
        }

        {
            // Both are scalars
            return Math.Abs(Convert.ToDouble(left) - Convert.ToDouble(right)) <= Epsilon;
        }
    }

    private static bool CompareSeries(List<IIndicatorResult> leftSeries, List<IIndicatorResult> rightSeries, int range, RangeEvaluationMode mode)
    {
        if (leftSeries.Count < 1 || rightSeries.Count < 1)
            return false;

        var pairsToCheck = Math.Min(range, Math.Min(leftSeries.Count, rightSeries.Count));
        var startIndexLeft = leftSeries.Count - pairsToCheck;
        var startIndexRight = rightSeries.Count - pairsToCheck;

        return RangeEvaluationHelper.Evaluate(pairsToCheck, mode, i =>
        {
            var lv = leftSeries[startIndexLeft + i].GetFieldValue("value");
            var rv = rightSeries[startIndexRight + i].GetFieldValue("value");
            return Math.Abs(lv - rv) <= Epsilon;
        });
    }

    private static bool CompareSeries(List<double> leftSeries, List<double> rightSeries, int range, RangeEvaluationMode mode)
    {
        if (leftSeries.Count < 1 || rightSeries.Count < 1)
            return false;

        var pairsToCheck = Math.Min(range, Math.Min(leftSeries.Count, rightSeries.Count));
        var startIndexLeft = leftSeries.Count - pairsToCheck;
        var startIndexRight = rightSeries.Count - pairsToCheck;

        return RangeEvaluationHelper.Evaluate(pairsToCheck, mode, i =>
        {
            return Math.Abs(leftSeries[startIndexLeft + i] - rightSeries[startIndexRight + i]) <= Epsilon;
        });
    }

    private static bool CompareSeriesScalar(List<IIndicatorResult> series, int range, RangeEvaluationMode mode, Func<double, bool> predicate)
    {
        if (series.Count < 1)
            return false;

        var startIndex = Math.Max(0, series.Count - range);
        var count = series.Count - startIndex;

        return RangeEvaluationHelper.Evaluate(count, mode, i =>
        {
            var value = series[startIndex + i].GetFieldValue("value");
            return predicate(value);
        });
    }

    private static bool CompareSeriesScalar(List<double> series, int range, RangeEvaluationMode mode, Func<double, bool> predicate)
    {
        if (series.Count < 1)
            return false;

        var startIndex = Math.Max(0, series.Count - range);
        var count = series.Count - startIndex;

        return RangeEvaluationHelper.Evaluate(count, mode, i =>
        {
            var value = series[startIndex + i];
            return predicate(value);
        });
    }
}
