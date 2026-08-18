using MarketViewer.Filters.Registry;
using MarketViewer.Filters.Interfaces;
using MarketViewer.Filters.Functions;

namespace MarketViewer.Filters.Functions.Indicators;

/// <summary>
/// Simple Moving Average function
/// </summary>
[FilterFunction("sma", Kind = FunctionKind.Series,
    Signature = "sma(period)", Snippet = "sma(14)",
    Description = "Simple moving average of close over the last `period` bars",
    Params = ["period"], Cost = 2, Selectivity = 0.5, Contexts = FilterContext.All)]
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
        int keep = IncrementalSeries.ReusablePointCount(prev, data, period - 1);
        if (keep < 0)
            return Execute(parameters, context);

        var result = IncrementalSeries.Seed(prev, keep, expectedCount);

        for (int i = (period - 1) + keep; i < data.Count; i++)
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
