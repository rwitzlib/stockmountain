using MarketViewer.Filters.Registry;
using MarketViewer.Filters.Interfaces;
using MarketViewer.Filters.Functions;

namespace MarketViewer.Filters.Functions.Indicators;

/// <summary>
/// Exponential Moving Average function
/// </summary>
[FilterFunction("ema", Kind = FunctionKind.Series,
    Signature = "ema(period)", Snippet = "ema(14)",
    Description = "Exponential moving average of close (SMA-seeded at bar `period`, alpha = 2/(period+1))",
    Params = ["period"], Cost = 2, Selectivity = 0.5, Contexts = FilterContext.All)]
public class EmaFunction : ISeriesFunction, IIncrementalSeriesFunction
{
    public string Name => "ema";

    public object Execute(object[] parameters, ExpressionContext context)
    {
        if (parameters.Length != 1)
            throw new ArgumentException("EMA function requires exactly 1 parameter (period)");

        var period = Convert.ToInt32(parameters[0]);
        var data = context.StockData.Results;

        if (data.Count < period)
            return new List<IIndicatorResult>(); // Not enough data

        var series = new List<IIndicatorResult>();
        var multiplier = 2.0 / (period + 1);

        // Calculate EMA for each point where we have enough data
        for (int i = period - 1; i < data.Count; i++)
        {
            double emaValue;
            if (i == period - 1)
            {
                // First EMA value is SMA
                emaValue = data.Take(period).Average(d => d.Close);
            }
            else
            {
                // Subsequent values use EMA formula
                var currentPrice = data[i].Close;
                var previousEma = ((SimpleIndicatorResult)series.Last()).Value;
                emaValue = (currentPrice - previousEma) * multiplier + previousEma;
            }

            series.Add(new SimpleIndicatorResult
            {
                Timestamp = data[i].Timestamp,
                Value = emaValue
            });
        }

        return series;
    }

    public object Append(object[] parameters, ExpressionContext context, object previousResult)
    {
        if (parameters.Length != 1)
            throw new ArgumentException("EMA function requires exactly 1 parameter (period)");

        var period = Convert.ToInt32(parameters[0]);
        var data = context.StockData.Results;

        if (data.Count < period)
            return new List<IIndicatorResult>();

        var prev = previousResult as List<IIndicatorResult> ?? new List<IIndicatorResult>();
        int expectedCount = data.Count - period + 1;
        int keep = IncrementalSeries.ReusablePointCount(prev, data, period - 1);
        if (keep <= 0)
            return Execute(parameters, context); // no reusable state (or data replaced): compute full

        var multiplier = 2.0 / (period + 1);
        var result = IncrementalSeries.Seed(prev, keep, expectedCount);

        // Continue the recurrence from the last kept point; the cached point after it is
        // provisional (its bar may have been forming when it was computed) and is recomputed.
        double previousEma = ((SimpleIndicatorResult)prev[keep - 1]).Value;
        int startIndex = (period - 1) + keep;

        for (int i = startIndex; i < data.Count; i++)
        {
            double currentPrice = data[i].Close;
            double emaValue = (currentPrice - previousEma) * multiplier + previousEma;
            previousEma = emaValue;

            result.Add(new SimpleIndicatorResult
            {
                Timestamp = data[i].Timestamp,
                Value = emaValue
            });
        }

        return result;
    }
}
