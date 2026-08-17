using System;
using System.Collections.Generic;
using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters;

/// <summary>
/// Comparison operators and <c>crosses_over/under</c> pair series operands by <em>position from the
/// end</em>, not by timestamp (bare <c>List&lt;double&gt;</c> operands — <c>.signal</c>, <c>slope()</c> —
/// carry no timestamps at all). That is only correct if every timestamped series a filter produces
/// is right-aligned with the context bars: its last point is for the last bar, the one before for the
/// bar before, and so on for as many points as the comparison touches. A series may start late
/// (warm-up) or be empty, but it may not stop early or skip a bar inside the compared tail —
/// otherwise a comparison silently reads a stale point.
///
/// This helper enforces that invariant at the producer, on both evaluation paths (direct
/// <c>IExpression.Evaluate</c> and the compiled <c>FilterSession</c>), for the tail the current
/// context can compare: the last <c>min(range, count)</c> points. Bare double series inherit the
/// guarantee from the timestamped series they were derived from. A violation is a bug in a function,
/// not a data or user error, so it throws (plan 14 follow-up 5).
///
/// The <c>time</c> field is exempt: it is a single point stamped with the evaluation clock, not a bar.
/// </summary>
public static class SeriesAlignment
{
    /// <summary>Range that a check covers when the context has none: a single point.</summary>
    private const int DefaultRange = 1;

    /// <summary>
    /// Verifies that <paramref name="series"/>'s compared tail lines up with the last bars of
    /// <paramref name="context"/>. Throws <see cref="InvalidOperationException"/> naming
    /// <paramref name="producer"/> if a point's timestamp differs from its positional bar.
    /// </summary>
    public static void AssertTail(List<IIndicatorResult> series, ExpressionContext context, string producer)
    {
        var bars = context.StockData?.Results;
        if (series.Count == 0 || bars is null || bars.Count == 0)
        {
            return;
        }

        var range = Math.Max(DefaultRange, context.CandleRange ?? DefaultRange);
        var tail = Math.Min(range, Math.Min(series.Count, bars.Count));

        for (int back = 1; back <= tail; back++)
        {
            var point = series[series.Count - back];
            var bar = bars[bars.Count - back];
            if (point.Timestamp != bar.Timestamp)
            {
                throw new InvalidOperationException(
                    $"Series produced by '{producer}' is not aligned with the bars it will be compared against: " +
                    $"point {back} from the end has timestamp {point.Timestamp} but bar {back} from the end is {bar.Timestamp} " +
                    $"(series has {series.Count} points, context has {bars.Count} bars, compared range {range}). " +
                    "Series must be right-aligned with the bars — one point per bar up to and including the last bar.");
            }
        }
    }
}
