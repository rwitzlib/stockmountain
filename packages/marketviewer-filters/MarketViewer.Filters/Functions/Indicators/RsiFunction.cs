using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Functions.Indicators;

/// <summary>
/// Relative Strength Index (RSI) indicator.
/// Usage: rsi(period, overbought, oversold, type)
/// - period: lookback window (e.g., 14)
/// - overbought: optional threshold (e.g., 70) - not used in computation but accepted for compatibility
/// - oversold: optional threshold (e.g., 30) - not used in computation but accepted for compatibility
/// - type: optional smoothing type: "wilders" (default), "ema", or "sma"
/// Returns a series (List<IIndicatorResult>) with Value in [0..100].
/// Example: rsi(14,70,30, ema) > 70
/// </summary>
public class RsiFunction : ISeriesFunction, IIncrementalSeriesFunction
{
    private static readonly string[] ValidTypes = ["wilders", "ema", "sma"];
    public string Name => "rsi";

    public object Execute(object[] parameters, ExpressionContext context)
    {
        if (parameters.Length != 4)
        {
            throw new ArgumentException("RSI requires 4 parameters: rsi(period, overbought, oversold, type)");
        }

        int period = Convert.ToInt32(parameters[0]);
        if (period < 2)
        {
            throw new ArgumentException("RSI period must be >= 2");
        }
        int overbought = Convert.ToInt32(parameters[1]); // Not used in computation
        int oversold = Convert.ToInt32(parameters[2]); // Not used in computation

        var type = parameters[3].ToString()?.ToLowerInvariant();

        if (!ValidTypes.Contains(type))
        {
            throw new ArgumentException($"Invalid RSI type: {type}. Valid types: {string.Join(", ", ValidTypes)}");
        }

        var data = context.StockData.Results;
        if (data.Count < period + 1)
        {
            return new List<IIndicatorResult>();
        }

        // Build close list for clarity
        var closes = data.Select(b => b.Close).ToList();

        // Compute initial average gain/loss over the first 'period' differences
        double gainSum = 0.0;
        double lossSum = 0.0;
        for (int i = 1; i <= period; i++)
        {
            var change = closes[i] - closes[i - 1];
            if (change >= 0)
            {
                gainSum += change;
            }
            else
            {
                lossSum += -change;
            }
        }

        double avgGain = gainSum / period;
        double avgLoss = lossSum / period;

        // For SMA, track rolling window sums using queues
        Queue<double>? gainQueue = null;
        Queue<double>? lossQueue = null;
        if (type == "sma")
        {
            gainQueue = new Queue<double>(period);
            lossQueue = new Queue<double>(period);
            // Seed with initial window values
            for (int i = 1; i <= period; i++)
            {
                var change = closes[i] - closes[i - 1];
                gainQueue.Enqueue(Math.Max(change, 0));
                lossQueue.Enqueue(Math.Max(-change, 0));
            }
        }

        double alpha = 2.0 / (period + 1.0);

        var series = new List<IIndicatorResult>
        {
            // First RSI value corresponds to index = period
            new RsiResult
            {
                Timestamp = data[period].Timestamp,
                Value = ComputeRsi(avgGain, avgLoss),
                Overbought = overbought,
                Oversold = oversold,
                AvgGain = avgGain,
                AvgLoss = avgLoss
            }
        };

        // Subsequent values
        for (int i = period + 1; i < closes.Count; i++)
        {
            var change = closes[i] - closes[i - 1];
            double gain = Math.Max(change, 0);
            double loss = Math.Max(-change, 0);

            switch (type)
            {
                case "wilders":
                    avgGain = (avgGain * (period - 1) + gain) / period;
                    avgLoss = (avgLoss * (period - 1) + loss) / period;
                    break;
                case "ema":
                    avgGain = (gain - avgGain) * alpha + avgGain; // EMA smoothing
                    avgLoss = (loss - avgLoss) * alpha + avgLoss;
                    break;
                case "sma":
                    // Maintain rolling window sums via queues
                    gainQueue!.Enqueue(gain);
                    lossQueue!.Enqueue(loss);
                    if (gainQueue.Count > period) gainSum -= gainQueue.Dequeue();
                    if (lossQueue.Count > period) lossSum -= lossQueue.Dequeue();
                    // Update sums with current additions
                    gainSum += gain;
                    lossSum += loss;
                    avgGain = gainSum / period;
                    avgLoss = lossSum / period;
                    break;
            }

            series.Add(new RsiResult
            {
                Timestamp = data[i].Timestamp,
                Value = ComputeRsi(avgGain, avgLoss),
                Overbought = overbought,
                Oversold = oversold,
                AvgGain = avgGain,
                AvgLoss = avgLoss
            });
        }

        return series;
    }

    private static double ComputeRsi(double avgGain, double avgLoss)
    {
        if (avgLoss <= 1e-12)
        {
            if (avgGain <= 1e-12) return 50.0; // flat
            return 100.0; // no losses
        }
        if (avgGain <= 1e-12)
        {
            return 0.0; // no gains
        }
        var rs = avgGain / avgLoss;
        return 100.0 - (100.0 / (1.0 + rs));
    }

    public object Append(object[] parameters, ExpressionContext context, object previousResult)
    {
        if (parameters.Length != 4)
            throw new ArgumentException("RSI requires 4 parameters: rsi(period, overbought, oversold, type)");

        int period = Convert.ToInt32(parameters[0]);
        double overbought = Convert.ToDouble(parameters[1]);
        double oversold = Convert.ToDouble(parameters[2]);
        string type = (parameters[3]?.ToString() ?? "wilders").ToLowerInvariant();
        if (period < 2) throw new ArgumentException("RSI period must be >= 2");

        var data = context.StockData.Results;
        if (data.Count < period + 1)
            return new List<IIndicatorResult>();

        var prev = previousResult as List<IIndicatorResult> ?? new List<IIndicatorResult>();
        int closesCount = data.Count;

        int expectedCount = closesCount - period;
        int toAdd = expectedCount - prev.Count;
        if (toAdd <= 0)
        {
            return prev;
        }

        // Seed from last known state
        if (prev.Count == 0)
        {
            return Execute(parameters, context);
        }

        var last = (RsiResult)prev.Last();
        double avgGain = last.AvgGain;
        double avgLoss = last.AvgLoss;
        int startIndex = period + prev.Count; // next close index to compute
        double alpha = 2.0 / (period + 1.0);

        var result = new List<IIndicatorResult>(expectedCount);
        result.AddRange(prev);

        for (int i = startIndex; i < closesCount; i++)
        {
            var change = data[i].Close - data[i - 1].Close;
            double gain = Math.Max(change, 0);
            double loss = Math.Max(-change, 0);

            switch (type)
            {
                case "wilders":
                    avgGain = (avgGain * (period - 1) + gain) / period;
                    avgLoss = (avgLoss * (period - 1) + loss) / period;
                    break;
                case "ema":
                    avgGain = (gain - avgGain) * alpha + avgGain;
                    avgLoss = (loss - avgLoss) * alpha + avgLoss;
                    break;
                case "sma":
                    double gainSum = 0.0, lossSum = 0.0;
                    for (int k = i - period + 1; k <= i; k++)
                    {
                        var ch = data[k].Close - data[k - 1].Close;
                        if (ch >= 0) gainSum += ch; else lossSum += -ch;
                    }
                    avgGain = gainSum / period;
                    avgLoss = lossSum / period;
                    break;
            }

            result.Add(new RsiResult
            {
                Timestamp = data[i].Timestamp,
                Value = ComputeRsi(avgGain, avgLoss),
                Overbought = overbought,
                Oversold = oversold,
                AvgGain = avgGain,
                AvgLoss = avgLoss
            });
        }

        return result;
    }
}

