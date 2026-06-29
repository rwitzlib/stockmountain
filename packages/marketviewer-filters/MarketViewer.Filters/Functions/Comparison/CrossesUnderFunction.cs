using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Functions.Comparison;

/// <summary>
/// Crosses below function - checks if series1 crossed below series2
/// Can optionally check within a candle range
/// </summary>
public class CrossesUnderFunction : IBooleanFunction
{
    public string Name => "crosses_under";

    public object Execute(object[] parameters, ExpressionContext context)
    {
        if (parameters.Length < 2)
            throw new ArgumentException("crosses_under function requires at least 2 parameters (series1, series2)");

        // Extract series values, handling both List<IIndicatorResult> and List<double>
        var series1Values = ExtractSeriesValues(parameters[0]);
        var series2Values = ExtractSeriesValues(parameters[1]);

        if (series1Values == null || series2Values == null)
        {
            // Fallback: no historical context -> cannot detect cross reliably
            return false;
        }

        // Align series to the same length (take the minimum length)
        var minLength = Math.Min(series1Values.Count, series2Values.Count);
        if (minLength < 2)
            return false;

        // If lengths differ, truncate the longer series from the beginning
        if (series1Values.Count > minLength)
        {
            series1Values = series1Values.Skip(series1Values.Count - minLength).ToList();
        }
        if (series2Values.Count > minLength)
        {
            series2Values = series2Values.Skip(series2Values.Count - minLength).ToList();
        }

        int range = context.CandleRange ?? 1;
        var startIndex = Math.Max(1, minLength - range);

        for (int i = startIndex; i < minLength; i++)
        {
            // Check if series1 crossed below series2
            if (series1Values[i - 1] >= series2Values[i - 1] && series1Values[i] < series2Values[i])
            {
                return true;
            }
        }
        return false;
    }

    private List<double>? ExtractSeriesValues(object? parameter)
    {
        if (parameter is List<double> doubleSeries)
        {
            return doubleSeries;
        }
        else if (parameter is List<IIndicatorResult> indicatorSeries)
        {
            return indicatorSeries.Select(r => r.GetFieldValue("value")).ToList();
        }
        return null;
    }
}
