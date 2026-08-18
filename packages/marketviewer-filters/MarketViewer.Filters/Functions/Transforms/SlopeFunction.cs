using MarketViewer.Filters.Registry;
using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Functions.Transforms;

/// <summary>
/// Computes the slope of a numeric series over a rolling window.
/// Accepts either List<IIndicatorResult> (uses value field) or List<double>.
/// Returns List<double> of slope values aligned to the end of each window.
/// Usage: slope(series [, period]) with default period = 5.
/// </summary>
[FilterFunction("slope", Kind = FunctionKind.Transform,
    Signature = "slope(series[, period])", Snippet = "slope(close, 5)",
    Description = "Linear regression slope of a series over a rolling window (default 5)",
    Params = ["series", "period?"], Cost = 1.5, Selectivity = 0.5, Contexts = FilterContext.Filters)]
public class SlopeFunction : ISeriesFunction, IIncrementalSeriesFunction
{
    public string Name => "slope";

    public object Execute(object[] parameters, ExpressionContext context)
    {
        if (parameters.Length is < 1 or > 2)
            throw new ArgumentException("slope() requires 1 or 2 parameters: slope(series [, period])");

        int period = 5;
        if (parameters.Length == 2)
        {
            period = Convert.ToInt32(parameters[1]);
            if (period < 2)
                throw new ArgumentException("slope() period must be >= 2");
        }

        // Normalize the first parameter into a double series
        List<double> values = parameters[0] switch
        {
            List<IIndicatorResult> series => series.Select(r => r.GetFieldValue("value")).ToList(),
            List<double> doubles => doubles,
            _ => throw new ArgumentException($"slope() first argument must be a series; got {parameters[0]?.GetType().Name ?? "null"}")
        };

        if (values.Count < period)
        {
            return new List<double>();
        }

        // Pre-compute x terms for linear regression over [0..period-1]
        // slope = (N*sum(xy) - sum(x)sum(y)) / (N*sum(x^2) - (sum(x))^2)
        int N = period;
        double sumX = (N - 1) * N / 2.0; // sum 0..N-1
        double sumX2 = (N - 1) * N * (2 * N - 1) / 6.0; // sum of squares 0..N-1
        double denom = N * sumX2 - sumX * sumX;
        if (Math.Abs(denom) < 1e-12)
        {
            return new List<double>();
        }

        var slopes = new List<double>(capacity: values.Count - period + 1);

        for (int end = period - 1; end < values.Count; end++)
        {
            double sumY = 0.0;
            double sumXY = 0.0;
            for (int i = 0; i < N; i++)
            {
                double y = values[end - (N - 1) + i];
                sumY += y;
                sumXY += i * y;
            }

            double slope = (N * sumXY - sumX * sumY) / denom;
            slopes.Add(slope);
        }

        return slopes;
    }

    public object Append(object[] parameters, ExpressionContext context, object previousResult)
    {
        // Determine period from parameters
        int period = 5;
        if (parameters.Length == 2)
        {
            period = Convert.ToInt32(parameters[1]);
            if (period < 2) throw new ArgumentException("slope() period must be >= 2");
        }

        // Normalize series input. Index lazily rather than projecting the whole series: Append is
        // called once per new bar and only ever needs the last ~period values, so a full
        // Select(...).ToList() here made the "incremental" path O(n) (PerformanceTests caught it).
        int valueCount;
        Func<int, double> valueAt;
        switch (parameters[0])
        {
            case List<IIndicatorResult> series:
                valueCount = series.Count;
                valueAt = i => series[i].GetFieldValue("value");
                break;
            case List<double> doubles:
                valueCount = doubles.Count;
                valueAt = i => doubles[i];
                break;
            default:
                throw new ArgumentException($"slope() first argument must be a series; got {parameters[0]?.GetType().Name ?? "null"}");
        }

        // Previous slopes list
        var prev = previousResult as List<double> ?? new List<double>();

        // prev[k] is the window ending at values[k + period - 1]. The input series has no
        // timestamps here, so reuse is by count: a rewind means rebuild. The last cached slope is
        // always recomputed — its window ended at an input value that may have belonged to a
        // still-forming bar when it was computed (see IncrementalSeries).
        int expectedCount = Math.Max(0, valueCount - period + 1);
        if (prev.Count > expectedCount)
        {
            return Execute(parameters, context);
        }
        int keep = Math.Max(0, prev.Count - 1);

        // Prepare constants for regression
        int N = period;
        double sumX = (N - 1) * N / 2.0;
        double sumX2 = (N - 1) * N * (2 * N - 1) / 6.0;
        double denom = N * sumX2 - sumX * sumX;
        if (Math.Abs(denom) < 1e-12) return prev;

        var result = new List<double>(expectedCount);
        result.AddRange(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(prev)[..keep]);

        // Start computing from the first window that is missing or provisional
        int startWindowEnd = period - 1 + keep;
        for (int end = startWindowEnd; end < valueCount; end++)
        {
            double sumY = 0.0;
            double sumXY = 0.0;
            for (int i = 0; i < N; i++)
            {
                double y = valueAt(end - (N - 1) + i);
                sumY += y;
                sumXY += i * y;
            }
            double slope = (N * sumXY - sumX * sumY) / denom;
            result.Add(slope);
        }

        return result;
    }
}
