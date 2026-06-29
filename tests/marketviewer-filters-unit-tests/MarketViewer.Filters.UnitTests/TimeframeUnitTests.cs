using MarketViewer.Contracts.Enums;
using Xunit;

namespace MarketViewer.Filters.UnitTests;

public class TimeframeUnitTests
{
    private readonly IndicatorExpressionEngine _engine;

    public TimeframeUnitTests()
    {
        _engine = new IndicatorExpressionEngine();
    }

    [Fact]
    public void TestTimerangeParsing()
    {
        // Act & Assert
        var result = _engine.ExtractTimeframeFromScript("adv() > 50 [5m, 1]");
        Assert.Equal(5, result?.Multiplier);
        Assert.Equal(Timespan.minute, result?.Timespan);
    }
}
