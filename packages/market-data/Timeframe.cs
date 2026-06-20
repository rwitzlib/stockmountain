namespace StockMountain.MarketData;

public readonly record struct Timeframe(int Multiplier, TimeframeUnit Unit)
{
    public void Validate()
    {
        if (Multiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Multiplier), Multiplier, "Multiplier must be positive.");
        }
    }

    public string ToPathSegment()
    {
        Validate();

        var unitSuffix = Unit switch
        {
            TimeframeUnit.Second => "s",
            TimeframeUnit.Minute => "m",
            TimeframeUnit.Hour => "h",
            TimeframeUnit.Day => "d",
            TimeframeUnit.Week => "w",
            TimeframeUnit.Month => "mo",
            TimeframeUnit.Quarter => "q",
            TimeframeUnit.Year => "y",
            _ => throw new ArgumentOutOfRangeException(nameof(Unit), Unit, "Unknown timeframe unit."),
        };

        return $"{Multiplier}{unitSuffix}";
    }

    public static Timeframe ParsePathSegment(string segment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(segment);

        var index = 0;
        while (index < segment.Length && char.IsDigit(segment[index]))
        {
            index++;
        }

        if (index == 0 || index >= segment.Length)
        {
            throw new FormatException($"Invalid timeframe path segment '{segment}'.");
        }

        if (!int.TryParse(segment.AsSpan(0, index), out var multiplier) || multiplier <= 0)
        {
            throw new FormatException($"Invalid timeframe path segment '{segment}'.");
        }

        var unit = segment[index..] switch
        {
            "s" => TimeframeUnit.Second,
            "m" => TimeframeUnit.Minute,
            "h" => TimeframeUnit.Hour,
            "d" => TimeframeUnit.Day,
            "w" => TimeframeUnit.Week,
            "mo" => TimeframeUnit.Month,
            "q" => TimeframeUnit.Quarter,
            "y" => TimeframeUnit.Year,
            _ => throw new FormatException($"Invalid timeframe path segment '{segment}'."),
        };

        return new Timeframe(multiplier, unit);
    }
}
