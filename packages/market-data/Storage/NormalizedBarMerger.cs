namespace StockMountain.MarketData.Storage;

public static class NormalizedBarMerger
{
    public static IReadOnlyList<NormalizedBar> Merge(
        IReadOnlyList<NormalizedBar> existing,
        IReadOnlyList<NormalizedBar> incoming)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(incoming);

        if (existing.Count == 0)
        {
            return Sort(incoming);
        }

        if (incoming.Count == 0)
        {
            return Sort(existing);
        }

        var merged = new Dictionary<DateTimeOffset, NormalizedBar>(existing.Count + incoming.Count);

        foreach (var bar in existing)
        {
            merged[bar.TimestampUtc] = bar;
        }

        foreach (var bar in incoming)
        {
            merged[bar.TimestampUtc] = bar;
        }

        return merged.Values.OrderBy(static bar => bar.TimestampUtc).ToArray();
    }

    private static IReadOnlyList<NormalizedBar> Sort(IReadOnlyList<NormalizedBar> bars) =>
        bars.Count <= 1
            ? bars
            : bars.OrderBy(static bar => bar.TimestampUtc).ToArray();
}
