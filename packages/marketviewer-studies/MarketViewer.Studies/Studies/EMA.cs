using MarketViewer.Contracts.Models.Indicator;
using MarketViewer.Contracts.Responses.Market;
using Polygon.Client.Models;

namespace MarketViewer.Studies.Studies;

/// <summary>
/// Exponential Moving Average
/// </summary>
public class EMA : IStudy
{
    public List<IndicatorPoint> Compute(string[] parameters, ref StocksResponse stocksResponse)
    {
        var series = new List<IndicatorPoint>();

        if (!Validate(parameters, ref stocksResponse, out var weight))
        {
            return [];
        }

        for (int i = 0; i < stocksResponse.Results.Count; i++)
        {
            if (i < weight - 1)
            {
                continue;
            }

            var value = GetExponentialMovingAverage(stocksResponse.Results, series, i, weight);

            series.Add(new IndicatorPoint
            {
                Timestamp = stocksResponse.Results[i].Timestamp,
                Value = value
            });
        }

        return series;
    }

    private static bool Validate(IReadOnlyList<object> parameters, ref StocksResponse stocksResponse, out int weight)
    {
        weight = 0;

        if (parameters is null || parameters.Count != 1)
        {
            return false;
        }

        if (int.TryParse(parameters[0].ToString(), out var _weight))
        {
            weight = _weight;
        }
        else
        {
            return false;
        }

        if (weight < 1 || weight > 1000)
        {
            return false;
        }

        if (stocksResponse.Results is not null && stocksResponse.Results.Count < weight)
        {
            return false;
        }

        return true;
    }

    private static float GetSimpleMovingAverage(IEnumerable<Bar> candles, int index, int weight)
    {
        var value = candles.ToList().GetRange(index - (weight - 1), weight).Sum(q => q.Close) / weight;

        return value;
    }

    private static float GetExponentialMovingAverage(IEnumerable<Bar> candles, List<IndicatorPoint> series, int index, int weight)
    {
        if (!series.Any())
        {
            return GetSimpleMovingAverage(candles, index, weight);
        }

        var smoothingFactor = 2f / (weight + 1);

        var value = candles.ToArray()[index].Close * smoothingFactor + series.Last().Value * (1 - smoothingFactor);

        return value;
    }
}