using StockMountain.MarketData;
using StockMountain.MarketData.Storage;

namespace StockMountain.MarketData.Tests;

public class SymbolTests
{
    [Fact]
    public void Create_NormalizesToUppercase()
    {
        var symbol = Symbol.Create("aapl");

        Assert.Equal("AAPL", symbol.Value);
    }

    [Fact]
    public void Create_RejectsInvalidTicker()
    {
        Assert.Throws<ArgumentException>(() => Symbol.Create("bad ticker"));
    }
}

public class TimeframeTests
{
    [Theory]
    [InlineData(1, TimeframeUnit.Minute, "1m")]
    [InlineData(5, TimeframeUnit.Minute, "5m")]
    [InlineData(1, TimeframeUnit.Day, "1d")]
    public void ToPathSegment_EncodesMultiplierAndUnit(int multiplier, TimeframeUnit unit, string expected)
    {
        var timeframe = new Timeframe(multiplier, unit);

        Assert.Equal(expected, timeframe.ToPathSegment());
    }

    [Theory]
    [InlineData("1m", 1, TimeframeUnit.Minute)]
    [InlineData("5m", 5, TimeframeUnit.Minute)]
    [InlineData("1d", 1, TimeframeUnit.Day)]
    public void ParsePathSegment_RoundTrips(string segment, int multiplier, TimeframeUnit unit)
    {
        var timeframe = Timeframe.ParsePathSegment(segment);

        Assert.Equal(new Timeframe(multiplier, unit), timeframe);
        Assert.Equal(segment, timeframe.ToPathSegment());
    }
}

public class BarObjectKeyTests
{
    [Fact]
    public void ForMonth_BuildsExpectedObjectKey()
    {
        var series = new BarSeriesKey(
            Symbol.Create("AAPL"),
            new Timeframe(1, TimeframeUnit.Minute),
            AdjustmentPolicy.SplitAdjusted);

        var objectKey = BarObjectKey.ForMonth(series, 2023, 1);

        Assert.Equal("bars/AAPL/1m/split-adjusted/2023/01.parquet", objectKey);
    }

    [Fact]
    public void TryParse_RoundTripsMonthlyObjectKey()
    {
        var series = new BarSeriesKey(
            Symbol.Create("BRK.B"),
            new Timeframe(1, TimeframeUnit.Day),
            AdjustmentPolicy.Unadjusted);
        var objectKey = BarObjectKey.ForMonth(series, 2024, 12);

        var parsed = BarObjectKey.TryParse(objectKey, out var parsedSeries, out var year, out var month);

        Assert.True(parsed);
        Assert.Equal(series, parsedSeries);
        Assert.Equal(2024, year);
        Assert.Equal(12, month);
    }
}

public class NormalizedBarParquetTests
{
    [Fact]
    public async Task WriteAndReadRange_PreservesBarsInRequestedWindow()
    {
        var bars = new[]
        {
            new NormalizedBar(
                new DateTimeOffset(2023, 1, 3, 14, 30, 0, TimeSpan.Zero),
                100.125m,
                101.25m,
                99.5m,
                100.75m,
                1_234_567,
                100.5m,
                4321),
            new NormalizedBar(
                new DateTimeOffset(2023, 1, 4, 14, 30, 0, TimeSpan.Zero),
                101m,
                102m,
                100m,
                101.5m,
                987_654,
                101.25m,
                2222),
        };

        await using var stream = new MemoryStream();
        await NormalizedBarParquetWriter.WriteAsync(stream, bars);
        stream.Position = 0;

        var readBars = new List<NormalizedBar>();
        await foreach (var bar in NormalizedBarParquetReader.ReadRangeAsync(
                           stream,
                           new DateTimeOffset(2023, 1, 3, 0, 0, 0, TimeSpan.Zero),
                           new DateTimeOffset(2023, 1, 3, 23, 59, 59, TimeSpan.Zero)))
        {
            readBars.Add(bar);
        }

        Assert.Single(readBars);
        Assert.Equal(bars[0], readBars[0]);
    }
}
