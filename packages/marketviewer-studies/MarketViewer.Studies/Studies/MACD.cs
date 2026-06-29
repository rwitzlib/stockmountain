using MarketViewer.Contracts.Models.Indicator;
using MarketViewer.Contracts.Responses.Market;
using Polygon.Client.Models;
using System.Collections.Generic;

namespace MarketViewer.Studies.Studies;

public class MACD : IStudy
{
    private static string[] ValidTypes { get; set; } = ["sma", "ema", "wilders"];

    public List<IndicatorPoint> Compute(string[] parameters, ref StocksResponse stocksResponse)
    {
        var series = new List<IndicatorPoint>();

        if (!Validate(parameters, stocksResponse, out var fastWeight, out var slowWeight, out var signalWeight, out var type))
        {
            return [];
        }

        List<float> fastValues = [];
        List<float> slowValues = [];
        List<float> signalValues = [];

        for (int i = 0; i < stocksResponse.Results.Count; i++)
        {
            if (i < fastWeight - 1)
            {
                continue;
            }

            var fastValue = type.ToLowerInvariant() switch
            {
                "sma" => GetSimpleMovingAverage(stocksResponse.Results, i, fastWeight),
                "ema" => GetExponentialMovingAverage(stocksResponse.Results, fastValues, i, fastWeight),
                "wilders" => GetWildersMovingAverage(stocksResponse.Results, fastValues, i, fastWeight),
                _ => throw new NotImplementedException()
            };
            fastValues.Add(fastValue);

            if (i < slowWeight - 1)
            {
                continue;
            }

            var slowValue = type.ToLowerInvariant() switch
            {
                "sma" => GetSimpleMovingAverage(stocksResponse.Results, i, slowWeight),
                "ema" => GetExponentialMovingAverage(stocksResponse.Results, slowValues, i, slowWeight),
                "wilders" => GetWildersMovingAverage(stocksResponse.Results, slowValues, i, slowWeight),
                _ => throw new NotImplementedException()
            };
            slowValues.Add(slowValue);

            var macdValue = fastValue - slowValue;

            series.Add(new MacdPoint
            {
                Value = macdValue,
                Timestamp = stocksResponse.Results[i].Timestamp,
            });

            if (series.Count < signalWeight)
            {
                continue;
            }

            var signalOffsetIndex = slowWeight - 1;
            var signalValue = type.ToLowerInvariant() switch
            {
                "sma" => GetSimpleMovingAverage(series, i - signalOffsetIndex, signalWeight),
                "ema" => GetExponentialMovingAverage(series, signalValues, i - signalOffsetIndex, signalWeight),
                "wilders" => GetWildersMovingAverage(series, signalValues, i - signalOffsetIndex, signalWeight),
                _ => throw new NotImplementedException()
            };
            signalValues.Add(signalValue);

            ((MacdPoint)series.Last()).Signal = signalValue;
            ((MacdPoint)series.Last()).Histogram = macdValue - signalValue;
        }

        return series;
    }

    private static bool Validate(
        IReadOnlyList<object> parameters,
        StocksResponse stocksResponse,
        out int fastWeight,
        out int slowWeight,
        out int signalWeight,
        out string type)
    {
        fastWeight = 0;
        slowWeight = 0;
        signalWeight = 0;
        type = string.Empty;

        if (parameters?.Count != 4)
        {
            return false;
        }

        if (int.TryParse(parameters[0].ToString(), out var _fastWeight))
        {
            fastWeight = _fastWeight;
        }
        else
        {
            return false;
        }

        if (int.TryParse(parameters[1].ToString(), out var _slowWeight))
        {
            slowWeight = _slowWeight;
        }
        else
        {
            return false;
        }

        if (int.TryParse(parameters[2].ToString(), out var _signalWeight))
        {
            signalWeight = _signalWeight;
        }
        else
        {
            return false;
        }

        if (ValidTypes.Contains(parameters[3].ToString().ToLowerInvariant()))
        {
            type = parameters[3].ToString().ToLowerInvariant() ?? string.Empty;
        }
        else
        {
            return false;
        }

        if (fastWeight < 1 || slowWeight < 1 || signalWeight < 1 || type == string.Empty)
        {
            return false;
        }

        if (stocksResponse.Results.Count < fastWeight 
            || stocksResponse.Results.Count < slowWeight 
            || stocksResponse.Results.Count < signalWeight)
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

    private static float GetSimpleMovingAverage(List<IndicatorPoint> data, int index, int weight)
    {
        var value = data.GetRange(index - (weight - 1), weight).Sum(q => q.Value) / weight;

        return value;
    }

    private static float GetExponentialMovingAverage(List<Bar> candles, List<float> data, int index, int weight)
    {
        if (!data.Any())
        {
            return GetSimpleMovingAverage(candles, index, weight);
        }

        var smoothingFactor = 2f / (weight + 1);

        var value = candles[index].Close * smoothingFactor + data.Last() * (1 - smoothingFactor);

        return value;
    }

    private static float GetExponentialMovingAverage(List<IndicatorPoint> data, List<float> series, int index, int weight)
    {
        if (!series.Any())
        {
            return GetSimpleMovingAverage(data, index, weight);
        }

        var smoothingFactor = 2f / (weight + 1);

        var value = data[index].Value * smoothingFactor + series.Last() * (1 - smoothingFactor);

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

    private static float GetWildersMovingAverage(List<IndicatorPoint> data, List<float> series, int index, int weight)
    {
        if (!series.Any())
        {
            return GetSimpleMovingAverage(data, index, weight);
        }

        var smoothingFactor = 1f / weight;

        var value = data[index].Value * smoothingFactor + series.Last() * (1 - smoothingFactor);

        return value;
    }
}