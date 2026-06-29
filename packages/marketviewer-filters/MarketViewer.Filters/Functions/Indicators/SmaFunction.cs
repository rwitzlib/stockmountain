using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Functions.Indicators;

/// <summary>
/// Simple Moving Average function
/// </summary>
public class SmaFunction : ISeriesFunction, IIncrementalSeriesFunction
{
    public string Name => "sma";

    public object Execute(object[] parameters, ExpressionContext context)
    {
        if (parameters.Length != 1)
            throw new ArgumentException("SMA function requires exactly 1 parameter (period)");

        var period = Convert.ToInt32(parameters[0]);
        var data = context.StockData.Results;

        if (data.Count < period)
            return new List<IIndicatorResult>(); // Not enough data

        var series = new List<IIndicatorResult>();

        for (int i = period - 1; i < data.Count; i++)
        {
            var sum = 0.0;
            for (int j = i - period + 1; j <= i; j++)
            {
                sum += data[j].Close;
            }
            var value = (double)(sum / period);
            series.Add(new SimpleIndicatorResult
            {
                Timestamp = data[i].Timestamp,
                Value = value
            });
        }

        return series;
    }

    public object Append(object[] parameters, ExpressionContext context, object previousResult)
    {
        if (parameters.Length != 1)
            throw new ArgumentException("SMA function requires exactly 1 parameter (period)");

        var period = Convert.ToInt32(parameters[0]);
        var data = context.StockData.Results;

        if (data.Count < period)
            return new List<IIndicatorResult>();

        var prev = previousResult as List<IIndicatorResult> ?? new List<IIndicatorResult>();
        int expectedCount = data.Count - period + 1;
        int toAdd = expectedCount - prev.Count;
        if (toAdd <= 0)
            return prev;

        var result = new List<IIndicatorResult>(expectedCount);
        result.AddRange(prev);

        for (int i = (period - 1) + prev.Count; i < data.Count; i++)
        {
            double sum = 0.0;
            for (int j = i - period + 1; j <= i; j++)
            {
                sum += data[j].Close;
            }
            var value = sum / period;
            result.Add(new SimpleIndicatorResult
            {
                Timestamp = data[i].Timestamp,
                Value = value
            });
        }

        return result;
    }
}
