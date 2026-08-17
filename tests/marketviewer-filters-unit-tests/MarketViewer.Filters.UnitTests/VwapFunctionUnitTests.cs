using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Filters.Interfaces;
using Massive.Client.Models;
using Xunit;

namespace MarketViewer.Filters.UnitTests;

/// <summary>
/// vwap() semantics that the golden fixtures exercise implicitly, pinned explicitly here:
/// 09:30 ET reset, pre-market carry of the previous session, vw fallback to typical price,
/// span-based session opening for larger timeframes, and incremental re-pricing of a forming bar.
/// (Values on real data vs an independent reference: GoldenIndicatorTests "vwap()" / "vwap(day)".)
/// </summary>
public class VwapFunctionUnitTests
{
    private static readonly IndicatorExpressionEngine Engine = new();
    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    private static readonly Timeframe Minute = new(1, Timespan.minute);

    private static long Et(int year, int month, int day, int hour, int minute)
    {
        var local = new DateTime(year, month, day, hour, minute, 0);
        return new DateTimeOffset(local, Eastern.GetUtcOffset(local)).ToUnixTimeMilliseconds();
    }

    private static Bar B(long t, float vw, float v, float h = 0, float l = 0, float c = 0) =>
        new() { Timestamp = t, Vwap = vw, Volume = v, High = h, Low = l, Close = c };

    [Fact]
    public void Resets_At_0930_And_Carries_Previous_Session_Through_PreMarket()
    {
        var bars = new List<Bar>
        {
            B(Et(2025, 6, 2, 9, 30), 10, 100),   // session 1 opens: vwap 10
            B(Et(2025, 6, 2, 9, 31), 20, 100),   // (10*100 + 20*100)/200 = 15
            B(Et(2025, 6, 2, 16, 5), 30, 100),   // after-hours continues: (1000+2000+3000)/300 = 20
            B(Et(2025, 6, 3, 8, 0), 40, 100),    // next-day pre-market still session 1: (6000+4000)/400 = 25
            B(Et(2025, 6, 3, 9, 30), 50, 100),   // session 2 opens: 50
        };
        var data = new StocksResponse { Results = bars };

        var s = Engine.EvaluateSeries("vwap()", data, Minute);
        Assert.Equal([10, 15, 20, 25, 50], s.Select(v => v!.Value));

        var day = Engine.EvaluateSeries("vwap(day)", data, Minute);
        Assert.Equal([10, 15, 20, 40, 45], day.Select(v => v!.Value)); // 6/3 resets at its first bar (08:00)
    }

    [Fact]
    public void Bars_Before_The_First_Open_Have_No_Value()
    {
        var bars = new List<Bar>
        {
            B(Et(2025, 6, 2, 8, 0), 10, 100),
            B(Et(2025, 6, 2, 9, 29), 20, 100),
            B(Et(2025, 6, 2, 9, 30), 30, 100),
        };
        var s = Engine.EvaluateSeries("vwap()", new StocksResponse { Results = bars }, Minute);
        Assert.Equal([null, null, 30.0], s);
        Assert.False(Engine.EvaluateScript("close > vwap()", new StocksResponse { Results = bars.Take(2).ToList() }, Minute));
    }

    [Fact]
    public void Falls_Back_To_Typical_Price_When_Bar_Has_No_Vw()
    {
        var bars = new List<Bar>
        {
            B(Et(2025, 6, 2, 9, 30), 0, 100, h: 12, l: 8, c: 10),  // typical = 10
            B(Et(2025, 6, 2, 9, 31), 20, 100),
        };
        var s = Engine.EvaluateSeries("vwap()", new StocksResponse { Results = bars }, Minute);
        Assert.Equal([10, 15], s.Select(v => v!.Value));
    }

    [Fact]
    public void Zero_Volume_Bar_Reports_Its_Own_Price_Until_Volume_Arrives()
    {
        var bars = new List<Bar> { B(Et(2025, 6, 2, 9, 30), 10, 0), B(Et(2025, 6, 2, 9, 31), 20, 100) };
        var s = Engine.EvaluateSeries("vwap()", new StocksResponse { Results = bars }, Minute);
        Assert.Equal([10, 20], s.Select(v => v!.Value));
    }

    [Theory]
    [InlineData(1, Timespan.hour, 9, 0, true)]     // 09:00-10:00 spans the open
    [InlineData(1, Timespan.hour, 8, 0, false)]    // 08:00-09:00 does not
    [InlineData(5, Timespan.minute, 9, 25, false)] // 09:25-09:30 ends exactly at the open: not after
    [InlineData(5, Timespan.minute, 9, 30, true)]
    [InlineData(1, Timespan.day, 0, 0, true)]      // daily bar at midnight spans the open
    public void Session_Opening_Uses_The_Bar_Span_Not_Just_Its_Start(int mult, Timespan span, int hour, int minute, bool opens)
    {
        var tf = new Timeframe(mult, span);
        Assert.Equal(opens, Functions.Indicators.VwapFunction.SessionOpenedBy(Et(2025, 6, 2, hour, minute), "session", tf) >= 0);
        Assert.True(Functions.Indicators.VwapFunction.SessionOpenedBy(Et(2025, 6, 2, hour, minute), "day", tf) >= 0);
    }

    [Fact]
    public void Daily_Bars_Are_Their_Own_Session()
    {
        var bars = new List<Bar> { B(Et(2025, 6, 2, 0, 0), 10, 100), B(Et(2025, 6, 3, 0, 0), 20, 100) };
        var s = Engine.EvaluateSeries("vwap()", new StocksResponse { Results = bars }, new Timeframe(1, Timespan.day));
        Assert.Equal([10, 20], s.Select(v => v!.Value));
    }

    [Fact]
    public void Incremental_RePrices_A_Forming_Last_Bar_And_Appends_New_Ones()
    {
        var tf = new Timeframe(5, Timespan.minute);
        var data = new StocksResponse
        {
            Results = [B(Et(2025, 6, 2, 9, 30), 10, 100), B(Et(2025, 6, 2, 9, 35), 20, 100)]
        };
        var session = Engine.Compile("vwap()");
        var first = (List<IIndicatorResult>)session.EvaluateIncrementalRaw(data, tf);
        Assert.Equal(15, first[^1].GetFieldValue());

        // The forming 09:35 candle changes in place (as UpdateLatestCandle does): vw 20 -> 30, volume 100 -> 300
        data.Results[^1].Vwap = 30;
        data.Results[^1].Volume = 300;
        var repriced = (List<IIndicatorResult>)session.EvaluateIncrementalRaw(data, tf);
        Assert.Equal(2, repriced.Count);
        Assert.Equal((10 * 100 + 30 * 300) / 400.0, repriced[^1].GetFieldValue(), 10);

        // Then a new candle arrives
        data.Results.Add(B(Et(2025, 6, 2, 9, 40), 40, 100));
        var appended = (List<IIndicatorResult>)session.EvaluateIncrementalRaw(data, tf);
        Assert.Equal(3, appended.Count);
        Assert.Equal((10 * 100 + 30 * 300 + 40 * 100) / 500.0, appended[^1].GetFieldValue(), 10);

        // And it all equals a from-scratch evaluation
        var full = Engine.EvaluateSeries("vwap()", data, tf);
        Assert.Equal(full.Select(v => v!.Value), appended.Select(p => p.GetFieldValue()));
    }

    [Theory]
    [InlineData("vwap(week)")]
    [InlineData("vwap(1, 2)")]
    public void Invalid_Arguments_Throw(string script)
    {
        var data = new StocksResponse { Results = [B(Et(2025, 6, 2, 9, 30), 10, 100)] };
        Assert.ThrowsAny<Exception>(() => Engine.EvaluateSeries(script, data, Minute));
    }

    [Fact]
    public void Bare_Vwap_Literal_Is_Gone()
    {
        var data = new StocksResponse { Results = [B(Et(2025, 6, 2, 9, 30), 10, 100, c: 11)] };
        Assert.ThrowsAny<Exception>(() => Engine.EvaluateScript("close > vwap", data, Minute));
        Assert.True(Engine.EvaluateScript("close > vwap()", data, Minute));
    }
}
