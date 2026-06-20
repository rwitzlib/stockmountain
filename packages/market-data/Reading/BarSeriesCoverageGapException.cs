using StockMountain.MarketData.Catalog;

namespace StockMountain.MarketData.Reading;

public sealed class BarSeriesCoverageGapException : Exception
{
    public BarSeriesCoverageGapException(IReadOnlyList<DateRange> gaps)
        : base(BuildMessage(gaps))
    {
        Gaps = gaps;
    }

    public IReadOnlyList<DateRange> Gaps { get; }

    private static string BuildMessage(IReadOnlyList<DateRange> gaps)
    {
        if (gaps.Count == 0)
        {
            return "Requested bar range is not fully covered.";
        }

        var ranges = string.Join(", ", gaps.Select(static gap => $"{gap.Start:o}..{gap.End:o}"));
        return $"Requested bar range is not fully covered. Missing: {ranges}";
    }
}
