using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Functions.Comparison;

/// <summary>
/// Shared argument handling and cross detection for <see cref="CrossesOverFunction"/> and
/// <see cref="CrossesUnderFunction"/>. Spec: docs/filters/crosses_over.md (Formula).
/// </summary>
internal static class CrossDetector
{
    /// <summary>
    /// Evaluates a cross of <paramref name="parameters"/>[0] through <paramref name="parameters"/>[1].
    /// Each argument is a series (<see cref="List{Double}"/> or <see cref="List{IIndicatorResult}"/>)
    /// or a number; a number is expanded to a constant series of the other argument's length so a
    /// level cross (`crosses_over(rsi(...), 30)`) is evaluated exactly like a series cross.
    /// Two numbers never cross. Fewer than 2 aligned values is false.
    /// </summary>
    public static bool Detect(string name, string signature, object[] parameters, ExpressionContext context, bool over)
    {
        if (parameters.Length != 2)
            throw new ArgumentException($"{name} requires exactly 2 arguments, got {parameters.Length}: {signature}");

        var first = Classify(name, signature, parameters[0], "series1");
        var second = Classify(name, signature, parameters[1], "series2");

        if (first.Series is null && second.Series is null)
        {
            // Two constants: nothing moves, so nothing crosses.
            return false;
        }

        var series1 = first.Series ?? Constant(first.Level, second.Series!.Count);
        var series2 = second.Series ?? Constant(second.Level, first.Series!.Count);

        // Align to the same length by truncating the longer series from the front.
        var length = Math.Min(series1.Count, series2.Count);
        if (length < 2)
            return false;

        var offset1 = series1.Count - length;
        var offset2 = series2.Count - length;

        var range = context.CandleRange ?? 1;
        var startIndex = Math.Max(1, length - range);

        for (var i = startIndex; i < length; i++)
        {
            var prev1 = series1[offset1 + i - 1];
            var prev2 = series2[offset2 + i - 1];
            var cur1 = series1[offset1 + i];
            var cur2 = series2[offset2 + i];

            var crossed = over
                ? prev1 <= prev2 && cur1 > cur2
                : prev1 >= prev2 && cur1 < cur2;

            if (crossed)
                return true;
        }

        return false;
    }

    private static List<double> Constant(double level, int count) =>
        Enumerable.Repeat(level, count).ToList();

    private static (List<double>? Series, double Level) Classify(string name, string signature, object? parameter, string position)
    {
        return parameter switch
        {
            List<double> doubles => (doubles, 0),
            List<IIndicatorResult> results => (results.Select(r => r.GetFieldValue("value")).ToList(), 0),
            double d => (null, d),
            float f => (null, f),
            int i => (null, i),
            long l => (null, l),
            decimal m => (null, (double)m),
            null => throw new ArgumentException($"{name}: {position} is missing — {signature}"),
            _ => throw new ArgumentException($"{name}: {position} must be a series or a number, got {parameter.GetType().Name} — {signature}")
        };
    }
}
