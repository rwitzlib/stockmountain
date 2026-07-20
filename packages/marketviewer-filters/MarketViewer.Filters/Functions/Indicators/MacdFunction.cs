using MarketViewer.Filters.Interfaces;
using MarketViewer.Contracts.Responses.Market;
using Massive.Client.Models;

namespace MarketViewer.Filters.Functions.Indicators;

/// <summary>
/// MACD (Moving Average Convergence Divergence) function
/// Returns MACD line, signal line, and histogram values
/// </summary>
public class MacdFunction : ISeriesFunction, IIncrementalSeriesFunction
{
    private static readonly string[] ValidTypes = ["sma", "ema", "wilders"];

    public string Name => "macd";

    public object Execute(object[] parameters, ExpressionContext context)
    {
        if (parameters.Length != 4)
            throw new ArgumentException("MACD function requires exactly 4 parameters (fastPeriod, slowPeriod, signalPeriod, type)");

        var fastPeriod = Convert.ToInt32(parameters[0]);
        var slowPeriod = Convert.ToInt32(parameters[1]);
        var signalPeriod = Convert.ToInt32(parameters[2]);
        var type = parameters[3].ToString()?.ToLowerInvariant();

        if (!ValidTypes.Contains(type))
            throw new ArgumentException($"Invalid MACD type: {type}. Valid types: {string.Join(", ", ValidTypes)}");

        var data = context.StockData.Results;

        if (data.Count < Math.Max(fastPeriod, Math.Max(slowPeriod, signalPeriod)))
            return new List<IIndicatorResult>(); // Not enough data

        var series = new List<IIndicatorResult>();
        var fastValues = new List<double>();
        var slowValues = new List<double>();
        var signalValues = new List<double>();

        for (int i = 0; i < data.Count; i++)
        {
            // Calculate fast MA
            if (i >= fastPeriod - 1)
            {
                var fastValue = CalculateMovingAverage(data, fastValues, i, fastPeriod, type!);
                fastValues.Add(fastValue);
            }
            else
            {
                continue; // Not enough data for fast MA
            }

            // Calculate slow MA
            if (i >= slowPeriod - 1)
            {
                var slowValue = CalculateMovingAverage(data, slowValues, i, slowPeriod, type!);
                slowValues.Add(slowValue);
            }
            else
            {
                continue; // Not enough data for slow MA
            }

            // Calculate MACD line (fast - slow)
            var macdValue = fastValues.Last() - slowValues.Last();

            var result = new MacdResult
            {
                Timestamp = data[i].Timestamp,
                Value = macdValue,
                Signal = 0, // Will be set later when we have enough data
                Histogram = 0, // Will be set later when we have signal
                FastMA = fastValues.Last(),
                SlowMA = slowValues.Last()
            };

            series.Add(result);

            // Calculate signal line (MA of MACD values)
            if (series.Count >= signalPeriod)
            {
                var signalValue = CalculateSignalAverage(series, signalValues, series.Count - 1, signalPeriod, type!);
                signalValues.Add(signalValue);

                // Update the last result with signal and histogram
                var lastResult = (MacdResult)series.Last();
                lastResult.Signal = signalValue;
                lastResult.Histogram = macdValue - signalValue;
                lastResult.SignalMA = signalValue;
            }
        }

        return series;
    }

    private double CalculateMovingAverage(List<Bar> data, List<double> previousValues, int index, int period, string type)
    {
        return type switch
        {
            "sma" => CalculateSMA(data, index, period),
            "ema" => CalculateEMA(data, previousValues, index, period),
            "wilders" => CalculateWilders(data, previousValues, index, period),
            _ => throw new NotImplementedException($"Moving average type not implemented: {type}")
        };
    }

    private double CalculateSignalAverage(List<IIndicatorResult> macdSeries, List<double> previousValues, int index, int period, string type)
    {
        var macdValues = macdSeries.Skip(Math.Max(0, index - period + 1)).Take(period)
                                  .Select(r => r.GetFieldValue("value")).ToList();

        return type switch
        {
            "sma" => macdValues.Average(),
            "ema" => CalculateEMAFromValues(macdValues, previousValues, period),
            "wilders" => CalculateWildersFromValues(macdValues, previousValues, period),
            _ => throw new NotImplementedException($"Signal average type not implemented: {type}")
        };
    }

    private double CalculateSMA(List<Bar> data, int index, int period)
    {
        var sum = 0.0;
        for (int i = index - period + 1; i <= index; i++)
        {
            sum += data[i].Close;
        }
        return sum / period;
    }

    private double CalculateEMA(List<Bar> data, List<double> previousValues, int index, int period)
    {
        if (!previousValues.Any())
        {
            return CalculateSMA(data, index, period);
        }

        var smoothingFactor = 2.0 / (period + 1);
        var currentPrice = data[index].Close;
        var previousEma = previousValues.Last();

        return currentPrice * smoothingFactor + previousEma * (1 - smoothingFactor);
    }

    private double CalculateWilders(List<Bar> data, List<double> previousValues, int index, int period)
    {
        if (!previousValues.Any())
        {
            return CalculateSMA(data, index, period);
        }

        var smoothingFactor = 1.0 / period;
        var currentPrice = data[index].Close;
        var previousWilders = previousValues.Last();

        return currentPrice * smoothingFactor + previousWilders * (1 - smoothingFactor);
    }

    private double CalculateEMAFromValues(List<double> values, List<double> previousValues, int period)
    {
        if (!previousValues.Any())
        {
            return values.Average();
        }

        var smoothingFactor = 2.0 / (period + 1);
        var currentValue = values.Last();
        var previousEma = previousValues.Last();

        return currentValue * smoothingFactor + previousEma * (1 - smoothingFactor);
    }

    private double CalculateWildersFromValues(List<double> values, List<double> previousValues, int period)
    {
        if (!previousValues.Any())
        {
            return values.Average();
        }

        var smoothingFactor = 1.0 / period;
        var currentValue = values.Last();
        var previousWilders = previousValues.Last();

        return currentValue * smoothingFactor + previousWilders * (1 - smoothingFactor);
    }

    public object Append(object[] parameters, ExpressionContext context, object previousResult)
    {
        if (parameters.Length != 4)
            throw new ArgumentException("MACD function requires exactly 4 parameters (fastPeriod, slowPeriod, signalPeriod, type)");

        var fastPeriod = Convert.ToInt32(parameters[0]);
        var slowPeriod = Convert.ToInt32(parameters[1]);
        var signalPeriod = Convert.ToInt32(parameters[2]);
        var type = parameters[3].ToString()?.ToLowerInvariant();

        if (!ValidTypes.Contains(type))
            throw new ArgumentException($"Invalid MACD type: {type}. Valid types: {string.Join(", ", ValidTypes)}");

        var data = context.StockData.Results;
        if (data.Count < Math.Max(fastPeriod, Math.Max(slowPeriod, signalPeriod)))
            return new List<IIndicatorResult>();

        var prev = previousResult as List<IIndicatorResult> ?? new List<IIndicatorResult>();
        if (prev.Count == 0)
        {
            return Execute(parameters, context);
        }

        int expectedCount = Math.Max(0, data.Count - slowPeriod + 1);
        int toAdd = expectedCount - prev.Count;
        if (toAdd <= 0)
            return prev;

        var result = new List<IIndicatorResult>(expectedCount);
        result.AddRange(prev);

        int startIndex = (slowPeriod - 1) + prev.Count; // data index for next MACD calc

        // get last states
        var last = (MacdResult)prev.Last();
        double lastFast = last.FastMA;
        double lastSlow = last.SlowMA;
        double lastSignal = last.Signal;

        // smoothing params
        double fastAlpha = 2.0 / (fastPeriod + 1.0);
        double slowAlpha = 2.0 / (slowPeriod + 1.0);
        double sigAlpha = 2.0 / (signalPeriod + 1.0);

        // helper for MA
        Func<int, int, double> smaClose = (idx, period) =>
        {
            double sum = 0.0;
            for (int j = idx - period + 1; j <= idx; j++) sum += context.StockData.Results[j].Close;
            return sum / period;
        };

        for (int i = startIndex; i < data.Count; i++)
        {
            double fastMA, slowMA;
            switch (type)
            {
                case "ema":
                    fastMA = (data[i].Close - lastFast) * fastAlpha + lastFast;
                    slowMA = (data[i].Close - lastSlow) * slowAlpha + lastSlow;
                    break;
                case "wilders":
                    fastMA = (data[i].Close - lastFast) * (1.0 / fastPeriod) + lastFast;
                    slowMA = (data[i].Close - lastSlow) * (1.0 / slowPeriod) + lastSlow;
                    break;
                case "sma":
                    fastMA = smaClose(i, fastPeriod);
                    slowMA = smaClose(i, slowPeriod);
                    break;
                default:
                    throw new NotImplementedException();
            }

            double macdValue = fastMA - slowMA;

            var macdResult = new MacdResult
            {
                Timestamp = data[i].Timestamp,
                Value = macdValue,
                Signal = 0,
                Histogram = 0,
                FastMA = fastMA,
                SlowMA = slowMA
            };

            // build macd history ref window for initial signal
            int countSoFar = prev.Count + (result.Count - prev.Count); // or simply result.Count
            countSoFar = result.Count; // number of macd points including prev

            if (countSoFar + 1 >= signalPeriod)
            {
                double signalValue;
                if (countSoFar + 1 == signalPeriod)
                {
                    // initial signal = SMA of last 'signalPeriod' macd values
                    // collect last signalPeriod-1 from tail of result plus current macdValue
                    var macdValues = result.Skip(Math.Max(0, result.Count - (signalPeriod - 1)))
                                           .Select(r => ((MacdResult)r).Value)
                                           .ToList();
                    macdValues.Add(macdValue);
                    signalValue = macdValues.Average();
                }
                else
                {
                    // subsequent smoothing
                    switch (type)
                    {
                        case "ema":
                            signalValue = (macdValue - lastSignal) * sigAlpha + lastSignal;
                            break;
                        case "wilders":
                            signalValue = (macdValue - lastSignal) * (1.0 / signalPeriod) + lastSignal;
                            break;
                        case "sma":
                            // SMA over macd last 'signalPeriod' values
                            var macds = result.Skip(Math.Max(0, result.Count - (signalPeriod - 1)))
                                              .Select(r => ((MacdResult)r).Value)
                                              .ToList();
                            macds.Add(macdValue);
                            signalValue = macds.Average();
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }

                macdResult.Signal = signalValue;
                macdResult.Histogram = macdValue - signalValue;
                macdResult.SignalMA = signalValue;
                lastSignal = signalValue;
            }

            // update states for next step
            lastFast = fastMA;
            lastSlow = slowMA;

            result.Add(macdResult);
        }

        return result;
    }
}
