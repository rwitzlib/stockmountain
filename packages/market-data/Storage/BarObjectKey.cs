namespace StockMountain.MarketData.Storage;

public static class BarObjectKey
{
    public const string RootPrefix = "bars";

    public static string ForMonth(BarSeriesKey series, int year, int month)
    {
        series.Validate();

        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), month, "Month must be between 1 and 12.");
        }

        if (year is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(year), year, "Year must be positive.");
        }

        return $"{RootPrefix}/{series.Symbol.Value}/{series.Timeframe.ToPathSegment()}/{ToPathSegment(series.AdjustmentPolicy)}/{year:D4}/{month:D2}.parquet";
    }

    public static bool TryParse(string objectKey, out BarSeriesKey series, out int year, out int month)
    {
        series = default;
        year = 0;
        month = 0;

        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return false;
        }

        var parts = objectKey.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 6
            || !parts[0].Equals(RootPrefix, StringComparison.Ordinal)
            || !parts[5].EndsWith(".parquet", StringComparison.Ordinal))
        {
            return false;
        }

        if (!Symbol.TryCreate(parts[1], out var symbol))
        {
            return false;
        }

        Timeframe timeframe;
        try
        {
            timeframe = Timeframe.ParsePathSegment(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (!TryParseAdjustmentPolicy(parts[3], out var adjustmentPolicy))
        {
            return false;
        }

        if (!int.TryParse(parts[4], out year) || !int.TryParse(parts[5][..^8], out month))
        {
            return false;
        }

        if (month is < 1 or > 12 || year is < 1)
        {
            return false;
        }

        series = new BarSeriesKey(symbol, timeframe, adjustmentPolicy);
        return true;
    }

    public static DateTimeOffset MonthPeriodStartUtc(int year, int month) =>
        new(year, month, 1, 0, 0, 0, TimeSpan.Zero);

    public static DateTimeOffset MonthPeriodEndUtc(int year, int month)
    {
        var start = MonthPeriodStartUtc(year, month);
        return start.Month == 12
            ? MonthPeriodStartUtc(year + 1, 1)
            : MonthPeriodStartUtc(year, month + 1);
    }

    private static string ToPathSegment(AdjustmentPolicy adjustmentPolicy) =>
        adjustmentPolicy switch
        {
            AdjustmentPolicy.SplitAdjusted => "split-adjusted",
            AdjustmentPolicy.Unadjusted => "unadjusted",
            _ => throw new ArgumentOutOfRangeException(nameof(adjustmentPolicy), adjustmentPolicy, "Unknown adjustment policy."),
        };

    private static bool TryParseAdjustmentPolicy(string segment, out AdjustmentPolicy adjustmentPolicy)
    {
        switch (segment)
        {
            case "split-adjusted":
                adjustmentPolicy = AdjustmentPolicy.SplitAdjusted;
                return true;
            case "unadjusted":
                adjustmentPolicy = AdjustmentPolicy.Unadjusted;
                return true;
            default:
                adjustmentPolicy = default;
                return false;
        }
    }
}
