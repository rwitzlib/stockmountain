using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Functions.Indicators;

/// <summary>
/// Exponential Moving Average function
/// </summary>
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
        int toAdd = expectedCount - prev.Count;
        if (toAdd <= 0)
            return prev;

        var multiplier = 2.0 / (period + 1);
        var result = new List<IIndicatorResult>(expectedCount);
        result.AddRange(prev);

        // Determine starting index and previous EMA value
        double previousEma;
        int startIndex;
        if (prev.Count == 0)
        {
            // No prior state; compute full
            return Execute(parameters, context);
        }
        else
        {
            previousEma = ((SimpleIndicatorResult)prev.Last()).Value;
            startIndex = (period - 1) + prev.Count;
        }

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
