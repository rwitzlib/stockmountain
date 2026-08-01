using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models.Strategy;
using Optimus.Adapter;
using Optimus.Adapter.Config;

namespace Optimus.UnitTests.Services;

public class AlpacaOrderMathTests
{
    private static readonly AlpacaAdapterConfig Config = new()
    {
        BackstopStopMultiplier = 3f,
        FallbackBackstopPercent = 25f
    };

    [Theory]
    [InlineData(1000f, 50f, 20)]
    [InlineData(1000f, 333f, 3)]
    [InlineData(100f, 250f, 0)]  // cannot afford a single share
    [InlineData(1000f, 0f, 0)]   // invalid price
    [InlineData(1000f, -5f, 0)]
    public void ComputeShares_ReturnsWholeAffordableShares(float positionSize, float price, int expected)
    {
        Assert.Equal(expected, AlpacaOrderMath.ComputeShares(positionSize, price));
    }

    [Fact]
    public void Backstop_PercentStop_RestsAtMultipleOfStopDistance()
    {
        // 2% logical stop on a $100 entry → $2 distance → backstop at 100 - 3*2 = $94.
        var stopLoss = new Exit { Type = ExitValueType.percent, Value = 2f };

        var stopPrice = AlpacaOrderMath.ComputeBackstopStopPrice(100f, 10, stopLoss, Config);

        Assert.Equal(94m, stopPrice);
    }

    [Fact]
    public void Backstop_FlatStop_DividesDollarsByShares()
    {
        // $50 stop on the whole 10-share position → $5/share → backstop at 100 - 3*5 = $85.
        var stopLoss = new Exit { Type = ExitValueType.flat, Value = 50f };

        var stopPrice = AlpacaOrderMath.ComputeBackstopStopPrice(100f, 10, stopLoss, Config);

        Assert.Equal(85m, stopPrice);
    }

    [Fact]
    public void Backstop_NegativeStopValue_IsNormalizedLikeExitEvaluator()
    {
        // Users enter stops with either sign; -2% and 2% mean the same thing.
        var stopLoss = new Exit { Type = ExitValueType.percent, Value = -2f };

        var stopPrice = AlpacaOrderMath.ComputeBackstopStopPrice(100f, 10, stopLoss, Config);

        Assert.Equal(94m, stopPrice);
    }

    [Fact]
    public void Backstop_NoStopLoss_FallsBackToConfigPercent()
    {
        // No logical stop → 25% fallback distance → backstop at 100 - 3*25 = $25.
        var stopPrice = AlpacaOrderMath.ComputeBackstopStopPrice(100f, 10, null, Config);

        Assert.Equal(25m, stopPrice);
    }

    [Fact]
    public void Backstop_ZeroValueStop_FallsBackToConfigPercent()
    {
        var stopLoss = new Exit { Type = ExitValueType.percent, Value = 0f };

        var stopPrice = AlpacaOrderMath.ComputeBackstopStopPrice(100f, 10, stopLoss, Config);

        Assert.Equal(25m, stopPrice);
    }

    [Fact]
    public void Backstop_DistanceBeyondEntry_ClampsToMinimumTick()
    {
        // 40% stop * 3 = 120% of entry — the raw stop is negative and must clamp, not error.
        var stopLoss = new Exit { Type = ExitValueType.percent, Value = 40f };

        var stopPrice = AlpacaOrderMath.ComputeBackstopStopPrice(100f, 10, stopLoss, Config);

        Assert.Equal(0.0001m, stopPrice);
    }

    [Theory]
    [InlineData(0f, 10)]
    [InlineData(-1f, 10)]
    [InlineData(100f, 0)]
    public void Backstop_InvalidInputs_ReturnNull(float entryPrice, int shares)
    {
        var stopLoss = new Exit { Type = ExitValueType.percent, Value = 2f };

        Assert.Null(AlpacaOrderMath.ComputeBackstopStopPrice(entryPrice, shares, stopLoss, Config));
    }

    [Theory]
    [InlineData("94.128", "94.12")]   // >= $1: 2 decimals, truncated toward zero
    [InlineData("1.005", "1.00")]
    [InlineData("0.34567", "0.3456")] // sub-dollar: 4 decimals
    [InlineData("-3.50", "0.0001")]   // clamps instead of submitting an invalid stop
    public void RoundStopPrice_MatchesAlpacaTickRules(string input, string expected)
    {
        var result = AlpacaOrderMath.RoundStopPrice(decimal.Parse(input));

        Assert.Equal(decimal.Parse(expected), result);
    }
}
