using MarketViewer.Filters.Registry;
using MarketViewer.Filters.Interfaces;
using MarketViewer.Filters.Functions;

namespace MarketViewer.Filters.Functions.Indicators;

/// <summary>
/// Average volume over the last <c>period</c> bars of the active timeframe (default 30),
/// including the current bar — i.e. an SMA of volume. On a daily timeframe this is the
/// classic "average daily volume"; on intraday timeframes it is average bar volume.
/// Usage: adv() or adv(period).
/// Golden-tested against tools/golden/compute_reference.py (rolling mean of volume).
/// </summary>
[FilterFunction("adv", Kind = FunctionKind.Series,
    Signature = "adv([period])", Snippet = "adv()",
    Description = "Average volume over the last `period` bars of the active timeframe including the current bar (default 30); classic ADV on [1d]",
    Params = ["period?"], Cost = 1, Selectivity = 0.4, Contexts = FilterContext.Filters)]
public class AdvFunction : ISeriesFunction, IIncrementalSeriesFunction
{
    public string Name => "adv";

    private const int DefaultPeriod = 30;

    public object Execute(object[] parameters, ExpressionContext context)
    {
        var period = ParsePeriod(parameters);
        var data = context.StockData.Results;

        if (data.Count < period)
            return new List<IIndicatorResult>(); // Not enough data

        var series = new List<IIndicatorResult>(data.Count - period + 1);
        double sum = 0.0;
        for (int i = 0; i < data.Count; i++)
        {
            sum += data[i].Volume;
            if (i >= period)
            {
                sum -= data[i - period].Volume;
            }
            if (i >= period - 1)
            {
                series.Add(new SimpleIndicatorResult
                {
                    Timestamp = data[i].Timestamp,
                    Value = sum / period
                });
            }
        }

        return series;
    }

    public object Append(object[] parameters, ExpressionContext context, object previousResult)
    {
        var period = ParsePeriod(parameters);
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
                sum += data[j].Volume;
            }
            result.Add(new SimpleIndicatorResult
            {
                Timestamp = data[i].Timestamp,
                Value = sum / period
            });
        }

        return result;
    }

    private static int ParsePeriod(object[] parameters)
    {
        if (parameters.Length > 1)
            throw new ArgumentException("ADV function can have up to 1 parameter (period)");

        var period = parameters.Length > 0 ? Convert.ToInt32(parameters[0]) : DefaultPeriod;
        if (period < 1)
            throw new ArgumentException("ADV period must be >= 1");
        return period;
    }
}
