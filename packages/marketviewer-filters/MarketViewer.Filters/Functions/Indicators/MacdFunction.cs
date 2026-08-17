using MarketViewer.Filters.Interfaces;
using Massive.Client.Models;

namespace MarketViewer.Filters.Functions.Indicators;

/// <summary>
/// MACD (Moving Average Convergence Divergence) function.
/// Returns MACD line (.value), signal line (.signal) and histogram (.histogram).
///
/// Warm-up contract (matches TA-Lib / charting packages and the golden reference in
/// tools/golden/compute_reference.py): fast/slow averages are SMA-seeded at bar period-1, the MACD
/// line exists from bar slow-1, the signal line is the same kind of average over the MACD values,
/// SMA-seeded from the first <c>signalPeriod</c> of them, so the FIRST emitted point is at bar
/// <c>slow + signal - 2</c>. All three fields are real on every emitted point — there are no
/// placeholder zeros during warm-up (comparisons and crosses against a fake 0 signal used to
/// fire spuriously; see plans/14-golden-filter-tests.md §Findings).
/// </summary>
public class MacdFunction : ISeriesFunction, IIncrementalSeriesFunction
{
    private static readonly string[] ValidTypes = ["sma", "ema", "wilders"];

    public string Name => "macd";

    private readonly record struct Params(int Fast, int Slow, int Signal, string Type)
    {
        /// <summary>Data index of the first emitted point.</summary>
        public int FirstIndex => Slow - 1 + Signal - 1;
    }

    public object Execute(object[] parameters, ExpressionContext context)
    {
        var p = ParseParameters(parameters);
        var data = context.StockData.Results;

        if (data.Count <= p.FirstIndex)
            return new List<IIndicatorResult>(); // Not enough data

        var series = new List<IIndicatorResult>(data.Count - p.FirstIndex);
        var macdValues = new List<double>(data.Count - p.Slow + 1);

        double fast = 0, slow = 0, signal = 0;
        for (int i = p.Fast - 1; i < data.Count; i++)
        {
            fast = Smooth(data, i, p.Fast, p.Type, i == p.Fast - 1 ? null : fast);

            if (i < p.Slow - 1)
                continue;

            slow = Smooth(data, i, p.Slow, p.Type, i == p.Slow - 1 ? null : slow);
            var macd = fast - slow;
            macdValues.Add(macd);

            if (macdValues.Count < p.Signal)
                continue; // signal not seeded yet — emit nothing rather than a placeholder

            signal = SmoothValues(macdValues, p.Signal, p.Type, macdValues.Count == p.Signal ? null : signal);

            series.Add(new MacdResult
            {
                Timestamp = data[i].Timestamp,
                Value = macd,
                Signal = signal,
                Histogram = macd - signal,
                FastMA = fast,
                SlowMA = slow,
                SignalMA = signal
            });
        }

        return series;
    }

    public object Append(object[] parameters, ExpressionContext context, object previousResult)
    {
        var p = ParseParameters(parameters);
        var data = context.StockData.Results;

        if (data.Count <= p.FirstIndex)
            return new List<IIndicatorResult>();

        var prev = previousResult as List<IIndicatorResult> ?? new List<IIndicatorResult>();
        if (prev.Count == 0)
            return Execute(parameters, context);

        int expectedCount = data.Count - p.FirstIndex;
        if (expectedCount - prev.Count <= 0)
            return prev;

        var result = new List<IIndicatorResult>(expectedCount);
        result.AddRange(prev);

        var last = (MacdResult)prev[^1];
        double fast = last.FastMA, slow = last.SlowMA, signal = last.SignalMA;

        for (int i = p.FirstIndex + prev.Count; i < data.Count; i++)
        {
            fast = Smooth(data, i, p.Fast, p.Type, fast);
            slow = Smooth(data, i, p.Slow, p.Type, slow);
            var macd = fast - slow;

            // The signal is already seeded (prev is non-empty), so only the recurrence is needed;
            // the "sma" type needs the last signalPeriod MACD values, which we can recompute from
            // price directly because SMA-type MACD has no recursive state.
            signal = p.Type == "sma"
                ? SmaMacdOverLastBars(data, i, p)
                : SmoothValuesStep(macd, p.Signal, p.Type, signal);

            result.Add(new MacdResult
            {
                Timestamp = data[i].Timestamp,
                Value = macd,
                Signal = signal,
                Histogram = macd - signal,
                FastMA = fast,
                SlowMA = slow,
                SignalMA = signal
            });
        }

        return result;
    }

    // ---- helpers

    private static Params ParseParameters(object[] parameters)
    {
        if (parameters.Length != 4)
            throw new ArgumentException("MACD function requires exactly 4 parameters (fastPeriod, slowPeriod, signalPeriod, type)");

        var fast = Convert.ToInt32(parameters[0]);
        var slow = Convert.ToInt32(parameters[1]);
        var signal = Convert.ToInt32(parameters[2]);
        var type = parameters[3].ToString()?.ToLowerInvariant() ?? "";

        if (!ValidTypes.Contains(type))
            throw new ArgumentException($"Invalid MACD type: {type}. Valid types: {string.Join(", ", ValidTypes)}");
        if (fast < 1 || slow < 1 || signal < 1)
            throw new ArgumentException("MACD periods must be >= 1");
        if (fast > slow)
            throw new ArgumentException("MACD fast period must not exceed the slow period");

        return new Params(fast, slow, signal, type);
    }

    /// <summary>One step of the price average: SMA seed when <paramref name="previous"/> is null, else the recurrence.</summary>
    private static double Smooth(List<Bar> data, int index, int period, string type, double? previous)
    {
        if (type == "sma" || previous is null)
            return SmaClose(data, index, period);

        var alpha = type == "ema" ? 2.0 / (period + 1) : 1.0 / period;
        return (data[index].Close - previous.Value) * alpha + previous.Value;
    }

    /// <summary>One step of the signal average over MACD values (seed when <paramref name="previous"/> is null).</summary>
    private static double SmoothValues(List<double> values, int period, string type, double? previous)
    {
        if (type == "sma" || previous is null)
        {
            double sum = 0.0;
            for (int j = values.Count - period; j < values.Count; j++) sum += values[j];
            return sum / period;
        }
        return SmoothValuesStep(values[^1], period, type, previous.Value);
    }

    private static double SmoothValuesStep(double current, int period, string type, double previous)
    {
        var alpha = type == "ema" ? 2.0 / (period + 1) : 1.0 / period;
        return (current - previous) * alpha + previous;
    }

    private static double SmaClose(List<Bar> data, int index, int period)
    {
        double sum = 0.0;
        for (int j = index - period + 1; j <= index; j++) sum += data[j].Close;
        return sum / period;
    }

    /// <summary>SMA-type signal at <paramref name="index"/>: mean of the last signal-period SMA-type MACD values.</summary>
    private static double SmaMacdOverLastBars(List<Bar> data, int index, Params p)
    {
        double sum = 0.0;
        for (int j = index - p.Signal + 1; j <= index; j++)
            sum += SmaClose(data, j, p.Fast) - SmaClose(data, j, p.Slow);
        return sum / p.Signal;
    }
}
