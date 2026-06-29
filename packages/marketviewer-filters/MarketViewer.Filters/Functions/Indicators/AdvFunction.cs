using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Functions.Indicators;

/// <summary>
/// Average Daily Volume function
/// </summary>
public class AdvFunction : ISeriesFunction
{
    public string Name => "adv";

    public object Execute(object[] parameters, ExpressionContext context)
    {
        if (parameters.Length > 1)
            throw new ArgumentException("ADV function can have up to 1 parameter (period)");

        var period = parameters.Any() ? Convert.ToInt32(parameters[0]) : 30;
        var data = context.StockData.Results;

        if (data.Count < period)
        {
            return new List<IIndicatorResult>(); // Not enough data
        }

        var series = new List<IIndicatorResult>
        {
            new SimpleIndicatorResult
            {
                Timestamp = data.Last().Timestamp,
                Value = data.Average(q => q.Volume)
            }
        };

        return series;
    }
}
