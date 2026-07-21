using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using Massive.Client.Models;
using Xunit;

namespace MarketViewer.Filters.UnitTests;

public class TimeFilterUnitTests
{
    private readonly IndicatorExpressionEngine _engine = new();
    private readonly Timeframe _timeframe = new(1, Timespan.minute);

    // 2026-01-05 is a Monday during EST (UTC-5)
    private static readonly DateTimeOffset SessionDate = new(2026, 1, 5, 0, 0, 0, TimeSpan.FromHours(-5));

    [Theory]
    [InlineData(9, 31, true)]
    [InlineData(10, 29, true)]
    [InlineData(10, 30, false)]
    [InlineData(15, 59, false)]
    public void Time_LessThan_Evaluates_Latest_Candle(int hour, int minute, bool expected)
    {
        var stockData = CreateStockData((hour, minute));

        var result = _engine.EvaluateScript("time < 10:30", stockData, _timeframe);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("time >= 9:30", 9, 30, true)]
    [InlineData("time >= 9:30", 9, 29, false)]
    [InlineData("time <= 10:00", 10, 0, true)]
    [InlineData("time = 9:30", 9, 30, true)]
    [InlineData("time != 9:30", 9, 31, true)]
    [InlineData("time > 15:00", 15, 30, true)]
    public void Time_Supports_Comparison_Operators(string script, int hour, int minute, bool expected)
    {
        var stockData = CreateStockData((hour, minute));

        var result = _engine.EvaluateScript(script, stockData, _timeframe);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("time >= 9:45 AND time < 10:30", 9, 44, false)]
    [InlineData("time >= 9:45 AND time < 10:30", 9, 45, true)]
    [InlineData("time >= 9:45 AND time < 10:30", 10, 15, true)]
    [InlineData("time >= 9:45 AND time < 10:30", 10, 30, false)]
    public void Time_Window_With_Logical_And(string script, int hour, int minute, bool expected)
    {
        var stockData = CreateStockData((hour, minute));

        var result = _engine.EvaluateScript(script, stockData, _timeframe);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("time.hour < 10", 9, 59, true)]
    [InlineData("time.hour < 10", 10, 0, false)]
    [InlineData("time.hour = 9", 9, 30, true)]
    [InlineData("time.minute = 30", 10, 30, true)]
    [InlineData("time.minute = 30", 10, 31, false)]
    public void Time_Supports_Hour_And_Minute_Field_Access(string script, int hour, int minute, bool expected)
    {
        var stockData = CreateStockData((hour, minute));

        var result = _engine.EvaluateScript(script, stockData, _timeframe);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Time_Combines_With_Price_Conditions()
    {
        var stockData = CreateStockData((9, 45));
        stockData.Results[^1].Close = 12f;

        Assert.True(_engine.EvaluateScript("close > 10 AND time < 10:30", stockData, _timeframe));
        Assert.False(_engine.EvaluateScript("close > 15 AND time < 10:30", stockData, _timeframe));
    }

    [Fact]
    public void Time_Works_With_Session_Incremental_Evaluation()
    {
        var stockData = CreateStockData((9, 30), (9, 31));
        var session = _engine.Compile("time < 9:33");

        var full = session.Evaluate(stockData, _timeframe);
        stockData.Results.Add(CreateBar(9, 32));
        var stillInside = session.EvaluateIncremental(stockData, _timeframe);
        stockData.Results.Add(CreateBar(9, 33));
        var outside = session.EvaluateIncremental(stockData, _timeframe);

        Assert.True(full);
        Assert.True(stillInside);
        Assert.False(outside);
    }

    [Theory]
    [InlineData(RangeEvaluationMode.All, 3)]
    [InlineData(RangeEvaluationMode.Any, 3)]
    public void Time_Is_The_Evaluation_Clock_Regardless_Of_Range_Modes(RangeEvaluationMode mode, int range)
    {
        // time is a single evaluation-clock value, not per-bar history, so range
        // modifiers have nothing extra to aggregate over: only the clock matters.
        var stockData = CreateStockData((9, 29), (9, 30), (9, 31));
        var script = $"time >= 9:30 [1m, {range}, {mode.ToString().ToLowerInvariant()}]";

        var result = _engine.EvaluateScript(script, stockData, _timeframe);

        Assert.True(result);
    }

    [Theory]
    [InlineData(10, 5, false)] // clock past the cutoff: stale last bar must not pass
    [InlineData(9, 55, true)]
    public void EvaluationTime_Overrides_Stale_Last_Bar(int clockHour, int clockMinute, bool expected)
    {
        // Thinly traded ticker whose last bar printed at 9:50.
        var stockData = CreateStockData((9, 49), (9, 50));
        var evaluationTime = SessionDate.AddHours(clockHour).AddMinutes(clockMinute);

        var result = _engine.EvaluateScript("time < 10:00", stockData, _timeframe, evaluationTime: evaluationTime);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Session_Clock_Advances_Without_New_Bars()
    {
        // The stale-ticker scenario: no new bars arrive, but the clock moves past
        // the cutoff. A cached first evaluation must not keep the gate open.
        var stockData = CreateStockData((9, 49), (9, 50));
        var session = _engine.Compile("time < 10:00");

        var beforeCutoff = session.Evaluate(stockData, _timeframe, evaluationTime: SessionDate.AddHours(9).AddMinutes(55));
        var afterCutoff = session.EvaluateIncremental(stockData, _timeframe, evaluationTime: SessionDate.AddHours(10).AddMinutes(5));

        Assert.True(beforeCutoff);
        Assert.False(afterCutoff);
    }

    [Fact]
    public void Session_Clock_Advances_For_Field_Access_Without_New_Bars()
    {
        var stockData = CreateStockData((9, 49), (9, 50));
        var session = _engine.Compile("time.hour = 9");

        var beforeCutoff = session.Evaluate(stockData, _timeframe, evaluationTime: SessionDate.AddHours(9).AddMinutes(55));
        var afterCutoff = session.EvaluateIncremental(stockData, _timeframe, evaluationTime: SessionDate.AddHours(10).AddMinutes(5));

        Assert.True(beforeCutoff);
        Assert.False(afterCutoff);
    }

    [Theory]
    [InlineData("time < 24:00")]
    [InlineData("time < 10:75")]
    public void Invalid_Time_Literals_Throw(string script)
    {
        Assert.Throws<InvalidOperationException>(() => _engine.ParseExpression(script));
    }

    private static Bar CreateBar(int hour, int minute) => new()
    {
        Timestamp = SessionDate.AddHours(hour).AddMinutes(minute).ToUnixTimeMilliseconds(),
        Open = 10f,
        High = 10f,
        Low = 10f,
        Close = 10f,
        Volume = 1000
    };

    private static StocksResponse CreateStockData(params (int Hour, int Minute)[] candleTimes) => new()
    {
        Results = candleTimes.Select(t => CreateBar(t.Hour, t.Minute)).ToList()
    };
}
