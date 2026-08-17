using MarketViewer.Filters.Interfaces;
using Massive.Client.Models;

namespace MarketViewer.Filters.Functions;

/// <summary>
/// Shared bookkeeping for <see cref="IIncrementalSeriesFunction.Append"/> implementations.
///
/// The incremental contract (see <see cref="Sessions.FilterSession"/> and the backtester's
/// <c>UpdateLatestCandle</c>): between two evaluations the bar list may grow at the end, and the
/// LAST bar may be mutated in place — it is a candle that is still forming and gets a new
/// close/high/low/volume every minute. A point computed for the last bar is therefore provisional.
///
/// A node is not necessarily evaluated on every bar (AND/OR short-circuit in the session skips
/// branches), so by the time it is evaluated again its last cached point may be for a bar that
/// has since finished forming and is no longer last. Hence the rule is not "recompute the last
/// point if it is for the last bar" but simply: <b>always recompute the last cached point</b>.
/// Every earlier point was computed while its bar was already final (it was not the last bar at
/// that time, or it was itself the "last point" then and has been recomputed since), so the
/// invariant "only the last cached point can be provisional" holds across skipped evaluations.
/// </summary>
internal static class IncrementalSeries
{
    /// <summary>
    /// How many leading points of <paramref name="prev"/> can be kept as-is, given that
    /// <c>prev[k]</c> was computed for <c>data[firstIndex + k]</c>: <c>prev.Count - 1</c> (the last
    /// point is provisional and must be recomputed), <c>0</c> when there is nothing cached, and
    /// <c>-1</c> when <paramref name="prev"/> cannot be reused at all (the data shrank or was
    /// replaced under us — rebuild with Execute).
    /// </summary>
    public static int ReusablePointCount(List<IIndicatorResult> prev, List<Bar> data, int firstIndex)
    {
        if (prev.Count == 0)
            return 0;

        int lastIndex = firstIndex + prev.Count - 1;
        if (lastIndex >= data.Count)
            return -1; // rewind

        if (prev[^1].Timestamp != data[lastIndex].Timestamp)
            return -1; // not the bars we priced

        return prev.Count - 1;
    }

    /// <summary>A new list seeded with the first <paramref name="keep"/> points of <paramref name="prev"/>.</summary>
    public static List<IIndicatorResult> Seed(List<IIndicatorResult> prev, int keep, int capacity)
    {
        var result = new List<IIndicatorResult>(Math.Max(capacity, keep));
        result.AddRange(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(prev)[..keep]);
        return result;
    }
}
