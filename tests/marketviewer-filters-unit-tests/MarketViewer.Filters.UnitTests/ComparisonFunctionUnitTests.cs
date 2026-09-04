using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Filters.Functions.Comparison;
using MarketViewer.Filters.Interfaces;
using Massive.Client.Models;
using Xunit;

namespace MarketViewer.Filters.UnitTests;

/// <summary>
/// crosses_over / crosses_under semantics pinned on hand-built bars: series-vs-series, numeric level
/// on either side (the case that silently returned false before 2026-09-04 and produced zero-trade
/// backtests for `crosses_over(rsi(14,30,70,wilders), 30)`), candle ranges, equality edges, and
/// argument errors. Real-data proof against the independent Python reference lives in
/// GoldenFilterOutcomeTests (`cross-over-rsi-level-30`, `cross-under-rsi-level-70-r3`,
/// `cross-over-level-lhs-70`, `cross-over-macd-hist-zero`, `cross-two-literals-never`).
/// </summary>
public class ComparisonFunctionUnitTests
{
    private static readonly IndicatorExpressionEngine Engine = new();
    private static readonly Timeframe Minute = new(1, Timespan.minute);

    private static StocksResponse Closes(params float[] closes) => new()
    {
        Results = closes.Select((c, i) => new Bar { Timestamp = 60_000L * (i + 1), Open = c, High = c, Low = c, Close = c, Volume = 1000 }).ToList()
    };

    private static bool Eval(string script, StocksResponse data) => Engine.EvaluateScript(script, data, Minute);

    private static ExpressionContext DirectContext() => new() { StockData = Closes(1, 2, 3), Timeframe = Minute, CandleRange = 1 };

    // ---------------------------------------------------------------- series vs series

    [Fact]
    public void Series_Cross_Fires_Only_On_The_Crossing_Bar()
    {
        // sma(2) of [10, 12, 8, 9, 20] = [-, 11, 10, 8.5, 14.5]; close vs sma(2): 12>11, 8<10, 9>8.5, 20>14.5
        var data = Closes(10, 12, 8, 9, 20);

        Assert.False(Eval("crosses_over(close, sma(2))", data));          // latest pair (9>8.5 → 20>14.5): already above
        Assert.True(Eval("crosses_over(close, sma(2))", Closes(10, 12, 8, 9)));   // 8<=10 then 9>8.5
        Assert.True(Eval("crosses_under(close, sma(2))", Closes(10, 12, 8)));     // 12>=11 then 8<10
        Assert.False(Eval("crosses_under(close, sma(2))", data));
    }

    // ---------------------------------------------------------------- numeric level (the regression)

    [Fact]
    public void Level_On_The_Right_Fires_When_Series_Rises_Through_It()
    {
        Assert.True(Eval("crosses_over(close, 105)", Closes(100, 104, 106)));   // 104<=105, 106>105
        Assert.False(Eval("crosses_over(close, 105)", Closes(100, 106, 108)));  // already above on the previous bar
        Assert.False(Eval("crosses_under(close, 105)", Closes(100, 104, 106)));
        Assert.True(Eval("crosses_under(close, 105)", Closes(110, 106, 104)));  // 106>=105, 104<105
    }

    [Fact]
    public void Level_On_The_Left_Is_The_Mirror_Cross()
    {
        // 105 "rises above" close when close drops through 105.
        Assert.True(Eval("crosses_over(105, close)", Closes(110, 106, 104)));
        Assert.False(Eval("crosses_over(105, close)", Closes(100, 104, 106)));
        Assert.True(Eval("crosses_under(105, close)", Closes(100, 104, 106)));
    }

    [Fact]
    public void Rsi_Level_Cross_Fires_On_The_Bar_Rsi_Reclaims_30()
    {
        // 16 falling closes push RSI(14) well under 30; each rising close then lifts it. The cross
        // fires on exactly the first bar whose RSI is > 30 while the previous bar's was <= 30, and
        // only on that bar — the same event the user-facing rsi(14,30,70,…) spelling must detect.
        var closes = new List<float>();
        for (var i = 0; i < 16; i++) closes.Add(100 - i);          // 100 … 85
        for (var i = 1; i <= 12; i++) closes.Add(85 + 1.5f * i);   // 86.5 … 103

        const string rsi = "rsi(14,30,70,wilders)";
        var fired = new List<int>();
        for (var n = 2; n <= closes.Count; n++)
        {
            var window = Closes(closes.Take(n).ToArray());
            var prevBelow = Eval($"{rsi} <= 30", Closes(closes.Take(n - 1).ToArray()));
            var nowAbove = Eval($"{rsi} > 30", window);
            var crossed = Eval($"crosses_over({rsi}, 30) [1m]", window);

            Assert.Equal(prevBelow && nowAbove, crossed);
            if (crossed) fired.Add(n);
        }

        Assert.Single(fired);
        Assert.True(Eval($"{rsi} < 30", Closes(closes.Take(16).ToArray())), "setup: RSI must be oversold after the decline");
    }

    [Fact]
    public void Two_Levels_Never_Cross_And_Do_Not_Throw()
    {
        var data = Closes(100, 102, 104);
        Assert.False(Eval("crosses_over(105, 100)", data));
        Assert.False(Eval("crosses_over(105, 100) [1m, 3]", data));
        Assert.False(Eval("crosses_under(100, 105) [, 3]", data));
    }

    // ---------------------------------------------------------------- ranges and edges

    [Fact]
    public void Candle_Range_Means_Any_Cross_In_The_Last_N_Bars()
    {
        var data = Closes(100, 104, 106, 107, 108);   // cross at bar 3 (104→106 through 105)

        Assert.False(Eval("crosses_over(close, 105) [1m]", data));
        Assert.False(Eval("crosses_over(close, 105) [1m, 2]", data));   // pairs (3,4),(4,5) only
        Assert.True(Eval("crosses_over(close, 105) [1m, 3]", data));    // pairs (2,3),(3,4),(4,5)
        Assert.True(Eval("crosses_over(close, 105) [1m, 50]", data));   // range beyond history is clamped
    }

    [Fact]
    public void Touching_From_Below_Does_Not_Fire_But_Sitting_On_The_Level_Then_Rising_Does()
    {
        Assert.False(Eval("crosses_over(close, 105)", Closes(100, 105, 104)));  // touched, retreated
        Assert.False(Eval("crosses_over(close, 105)", Closes(100, 104, 105)));  // equality is not "above"
        Assert.True(Eval("crosses_over(close, 105)", Closes(100, 105, 106)));   // equality counts as "below" before
    }

    [Fact]
    public void Fewer_Than_Two_Bars_Is_False()
    {
        Assert.False(Eval("crosses_over(close, 105)", Closes(106)));
        Assert.False(Eval("crosses_over(close, sma(2))", Closes(100, 106)));   // sma(2) has one value
    }

    [Fact]
    public void Unsupported_Argument_Types_Throw_With_The_Signature()
    {
        var context = DirectContext();
        var series = new List<double> { 1, 2, 3 };

        var ex = Assert.Throws<ArgumentException>(() => new CrossesOverFunction().Execute([series, "thirty"], context));
        Assert.Contains("crosses_over(series1, series2)", ex.Message);

        Assert.Throws<ArgumentException>(() => new CrossesUnderFunction().Execute([series], context));
        Assert.Throws<ArgumentException>(() => new CrossesOverFunction().Execute([series, 30.0, 0.0], context));   // extra argument is not ignored
        var viaEngine = Assert.Throws<ArgumentException>(() => Eval("crosses_over(close, 30, 0)", Closes(100, 104, 106)));   // surfaces through the engine unwrapped
        Assert.Contains("exactly 2 arguments", viaEngine.Message);
        Assert.Throws<ArgumentException>(() => new CrossesOverFunction().Execute([null!, series], context));
    }

    [Fact]
    public void Direct_Execute_Accepts_Every_Numeric_Type()
    {
        var context = DirectContext();
        var series = new List<double> { 100, 104, 106 };
        var fn = new CrossesOverFunction();

        Assert.True((bool)fn.Execute([series, 105.0], context));
        Assert.True((bool)fn.Execute([series, 105], context));
        Assert.True((bool)fn.Execute([series, 105L], context));
        Assert.True((bool)fn.Execute([series, 105f], context));
        Assert.True((bool)fn.Execute([series, 105m], context));
    }
}
