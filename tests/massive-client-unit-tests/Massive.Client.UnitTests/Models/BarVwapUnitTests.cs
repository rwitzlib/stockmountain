using FluentAssertions;
using Massive.Client.Models;

namespace Massive.Client.UnitTests.Models;

/// <summary>Plan 14 follow-up 7: forming/aggregated candle VWAP is Σ(vw·v)/Σv, not (c+h+l)/3.</summary>
public class BarVwapUnitTests
{
    private static Bar Minute(float vwap, float volume, float close = 0, float high = 0, float low = 0) =>
        new() { Vwap = vwap, Volume = volume, Close = close, High = high, Low = low };

    [Fact]
    public void Merge_Is_Volume_Weighted()
    {
        var current = Minute(vwap: 100f, volume: 1000f);
        var incoming = Minute(vwap: 103f, volume: 3000f);

        BarVwap.Merge(current, incoming, mergedTypicalPrice: 999f).Should().BeApproximately(102.25f, 1e-4f);
    }

    [Fact]
    public void Merge_Recurrence_Equals_Aggregate_Over_The_Same_Minutes()
    {
        var minutes = new List<Bar>();
        var rng = new Random(7);
        for (int i = 0; i < 390; i++)
        {
            minutes.Add(Minute(vwap: 150f + (float)rng.NextDouble() * 5f, volume: (float)rng.Next(0, 50_000)));
        }

        // fold minute-by-minute the way UpdateLatestCandle does
        var candle = minutes[0].Clone();
        foreach (var m in minutes.Skip(1))
        {
            candle.Vwap = BarVwap.Merge(candle, m, 0f);
            candle.Volume += m.Volume;
        }

        var aggregate = BarVwap.Aggregate(minutes, 0f);
        candle.Vwap.Should().BeApproximately(aggregate, Math.Abs(aggregate) * 1e-5f);

        // and both match the double-precision definition
        double expected = minutes.Sum(m => (double)m.Vwap * m.Volume) / minutes.Sum(m => (double)m.Volume);
        aggregate.Should().BeApproximately((float)expected, (float)Math.Abs(expected) * 1e-6f);
    }

    [Fact]
    public void Merge_With_ZeroVolume_Incoming_Leaves_Vwap_Unchanged()
    {
        var current = Minute(vwap: 100f, volume: 1000f);
        var incoming = Minute(vwap: 0f, volume: 0f, close: 130f, high: 140f, low: 120f);

        BarVwap.Merge(current, incoming, mergedTypicalPrice: 999f).Should().Be(100f);
    }

    [Fact]
    public void Merge_And_Aggregate_Fall_Back_To_Typical_Price_When_Nothing_Has_Volume()
    {
        var a = Minute(vwap: 0f, volume: 0f);
        var b = Minute(vwap: 0f, volume: 0f);

        BarVwap.Merge(a, b, mergedTypicalPrice: 42f).Should().Be(42f);
        BarVwap.Aggregate([a, b], fallbackTypicalPrice: 42f).Should().Be(42f);
        BarVwap.Aggregate([], fallbackTypicalPrice: 42f).Should().Be(42f);
    }

    [Fact]
    public void Bars_Without_Vwap_But_With_Volume_Contribute_Their_Typical_Price()
    {
        // Massive minute bars always carry vw; a synthetic bar without one still weighs in at (c+h+l)/3.
        var withVwap = Minute(vwap: 100f, volume: 1000f);
        var noVwap = Minute(vwap: 0f, volume: 1000f, close: 104f, high: 106f, low: 98f); // typical 102.6667

        var expected = (100f * 1000f + (104f + 106f + 98f) / 3f * 1000f) / 2000f;
        BarVwap.Merge(withVwap, noVwap, 0f).Should().BeApproximately(expected, 1e-4f);
        BarVwap.Aggregate([withVwap, noVwap], 0f).Should().BeApproximately(expected, 1e-4f);
    }

    [Fact]
    public void TypicalPrice_Is_Close_High_Low_Over_Three()
    {
        BarVwap.TypicalPrice(new Bar { Close = 10, High = 12, Low = 8 }).Should().Be(10f);
    }
}
