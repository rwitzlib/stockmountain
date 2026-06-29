using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Functions.Transforms;

/// <summary>
/// Computes the slope of a numeric series over a rolling window.
/// Accepts either List<IIndicatorResult> (uses value field) or List<double>.
/// Returns List<double> of slope values aligned to the end of each window.
/// Usage: slope(series [, period]) with default period = 5.
/// </summary>
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

        // Normalize series input
        List<double> values = parameters[0] switch
        {
            List<IIndicatorResult> series => series.Select(r => r.GetFieldValue("value")).ToList(),
            List<double> doubles => doubles,
            _ => throw new ArgumentException($"slope() first argument must be a series; got {parameters[0]?.GetType().Name ?? "null"}")
        };

        // Previous slopes list
        var prev = previousResult as List<double> ?? new List<double>();

        // Compute expected output count and how many new points to add
        int expectedCount = Math.Max(0, values.Count - period + 1);
        int toAdd = expectedCount - prev.Count;
        if (toAdd <= 0)
        {
            // Nothing new; return previous
            return prev;
        }

        // Prepare constants for regression
        int N = period;
        double sumX = (N - 1) * N / 2.0;
        double sumX2 = (N - 1) * N * (2 * N - 1) / 6.0;
        double denom = N * sumX2 - sumX * sumX;
        if (Math.Abs(denom) < 1e-12) return prev;

        var result = new List<double>(expectedCount);
        result.AddRange(prev);

        // Start computing from first missing window
        int startWindowEnd = period - 1 + prev.Count;
        for (int end = startWindowEnd; end < values.Count; end++)
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
            result.Add(slope);
        }

        return result;
    }
}
