using MarketViewer.Contracts.Models.Indicator;
using MarketViewer.Contracts.Responses.Market;
using Polygon.Client.Models;

namespace MarketViewer.Studies.Studies;

/// <summary>
/// Moving Average Mean Reversion (MAMR) study implementation
/// 
/// This indicator is a mean regersion strategy that uses 
/// moving averages to identify potential reversals in the price of a stock.
/// 
/// Parameters:
/// - Weight: The period for the moving average
/// - Type: The type of moving average to use (sma, ema, wilders)
/// - Period: The period for the reversion
///
/// 
/// Example:
/// - For a 200-period simple moving average with a 180-period reversion, use: 200, sma, 180
/// </summary>
public class MAMR : IStudy
{
    private static string[] ValidTypes { get; set; } = ["sma", "ema", "wilders"];

    public List<IndicatorPoint> Compute(string[] parameters, ref StocksResponse stocksResponse)
    {
        var series = new List<IndicatorPoint>();

        if (!Validate(parameters, stocksResponse, out var movingAverageWeight, out var type, out var reversionPeriod))
        {
            return [];
        }

        List<float> movingAverage = [];
        List<IndicatorPoint> meanReversionValues = [];

        // First pass: Calculate moving averages
        for (int i = 0; i < stocksResponse.Results.Count; i++)
        {
            if (i < movingAverageWeight - 1)
            {
                continue;
            }

            var movingAverageValue = type.ToLowerInvariant() switch
            {
                "sma" => GetSimpleMovingAverage(stocksResponse.Results, i, movingAverageWeight),
                "ema" => GetExponentialMovingAverage(stocksResponse.Results, movingAverage, i, movingAverageWeight),
                "wilders" => GetWildersMovingAverage(stocksResponse.Results, movingAverage, i, movingAverageWeight),
                _ => throw new NotImplementedException()
            };
            movingAverage.Add(movingAverageValue);

            var meanReversion = stocksResponse.Results[i].Close - movingAverageValue;

            meanReversionValues.Add(new IndicatorPoint
            {
                Value = meanReversion,
                Timestamp = stocksResponse.Results[i].Timestamp
            });
        }


        var standardDeviationWeight = 12;
        for (int i = standardDeviationWeight - 1; i < meanReversionValues.Count; i++)
        {
            var periodValues = meanReversionValues.Skip((i - standardDeviationWeight) + 1).Take(standardDeviationWeight).Select(q => q.Value);
            
            // Calculate standard deviation
            var mean = periodValues.Average();
            var sumOfSquares = periodValues.Sum(x => Math.Pow(x - mean, 2));
            var standardDeviation = (float)Math.Sqrt(sumOfSquares / standardDeviationWeight);
            
            series.Add(new IndicatorPoint
            {
                Value = standardDeviation,
                Timestamp = meanReversionValues[i].Timestamp
            });
        }

        return series;
    }

    #region Private Methods

    private static bool Validate(
        IReadOnlyList<object> parameters,
        StocksResponse stocksResponse,
        out int movingAverageWeight,
        out string type,
        out int reversionPeriod)
    {
        movingAverageWeight = 0;
        reversionPeriod = 0;
        type = string.Empty;

        if (parameters?.Count != 3)
        {
            return false;
        }

        if (int.TryParse(parameters[0].ToString(), out var _movingAverageWeight))
        {
            movingAverageWeight = _movingAverageWeight;
        }
        else
        {
            return false;
        }

        if (ValidTypes.Contains(parameters[1].ToString().ToLowerInvariant()))
        {
            type = parameters[1].ToString().ToLowerInvariant() ?? string.Empty;
        }
        else
        {
            return false;
        }

        if (int.TryParse(parameters[2].ToString(), out var _reversionPeriod))
        {
            reversionPeriod = _reversionPeriod;
        }
        else
        {
            return false;
        }

        if (movingAverageWeight < 1 || movingAverageWeight > 1000 || reversionPeriod < 1 || reversionPeriod > 365 || type == string.Empty)
        {
            return false;
        }

        if (stocksResponse.Results.Count < movingAverageWeight
            || stocksResponse.Results.Count < reversionPeriod)
        {
            return false;
        }

        return true;
    }

    private static float GetSimpleMovingAverage(List<Bar> candles, int index, int weight)
    {
        var value = candles.GetRange(index - (weight - 1), weight).Sum(q => q.Close) / weight;

        return value;
    }

    private static float GetExponentialMovingAverage(List<Bar> candles, List<float> series, int index, int weight)
    {
        if (!series.Any())
        {
            return GetSimpleMovingAverage(candles, index, weight);
        }

        var smoothingFactor = 2f / (weight + 1);

        var value = candles.ToArray()[index].Close * smoothingFactor + series.Last() * (1 - smoothingFactor);

        return value;
    }

    private static float GetWildersMovingAverage(List<Bar> candles, List<float> series, int index, int weight)
    {
        if (!series.Any())
        {
            return GetSimpleMovingAverage(candles, index, weight);
        }

        var smoothingFactor = 1f / weight;

        var value = candles[index].Close * smoothingFactor + series.Last() * (1 - smoothingFactor);

        return value;
    }

    #endregion
}