using System;
using MarketViewer.Filters;
using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Operators.Comparison;

/// <summary>
/// Greater than operator (>)
/// </summary>
public class GreaterThanOperator : IComparisonOperator
{
    public string Symbol => ">";
    public int Precedence => 6;
    public bool IsBinary => true;
    public bool IsUnary => false;

    public object Execute(object? left, object right, ExpressionContext context)
    {
        ArgumentNullException.ThrowIfNull(left);

        var range = context.CandleRange ?? 1;
        var mode = context.RangeEvaluationMode;

        if (left is List<IIndicatorResult> leftSeries && right is List<IIndicatorResult> rightSeries)
        {
            return CompareSeries(leftSeries, rightSeries, range, mode);
        }

        if (left is List<double> leftDoubleSeries && right is List<double> rightDoubleSeries)
        {
            return CompareSeries(leftDoubleSeries, rightDoubleSeries, range, mode);
        }

        if (left is List<IIndicatorResult> leftSeriesScalar)
        {
            var rightScalar = Convert.ToDouble(right);
            return CompareSeriesScalar(leftSeriesScalar, range, mode, value => value > rightScalar);
        }

        if (left is List<double> leftDoubleSeriesScalar)
        {
            var rightScalar = Convert.ToDouble(right);
            return CompareSeriesScalar(leftDoubleSeriesScalar, range, mode, value => value > rightScalar);
        }

        if (right is List<IIndicatorResult> rightSeriesScalar)
        {
            var leftScalar = Convert.ToDouble(left);
            return CompareSeriesScalar(rightSeriesScalar, range, mode, value => leftScalar > value);
        }

        if (right is List<double> rightDoubleSeriesScalar)
        {
            var leftScalar = Convert.ToDouble(left);
            return CompareSeriesScalar(rightDoubleSeriesScalar, range, mode, value => leftScalar > value);
        }

        {
            return Convert.ToDouble(left) > Convert.ToDouble(right);
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
            return lv > rv;
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
            return leftSeries[startIndexLeft + i] > rightSeries[startIndexRight + i];
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
