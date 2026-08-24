using Backtest.Lambda.Utilities;
using FluentAssertions;
using Xunit;

namespace Backtest.Lambda.UnitTests.Utilities;

public class CreditMeterUnitTests
{
    [Theory]
    [InlineData(2f, 50, 1f)]      // 2 GB × 50s = 100 GB-s = 1 credit
    [InlineData(10f, 30, 3f)]     // 10 GB × 30s = 300 GB-s = 3 credits
    [InlineData(2f, 0, 0f)]
    public void Compute_ScalesGbSecondsToCredits(float memoryGb, double seconds, float expected)
    {
        CreditMeter.Compute(memoryGb, seconds).Should().Be(expected);
    }

    [Fact]
    public void EstimateForRange_SingleDay_RoundsUpToOneCredit()
    {
        var day = DateTimeOffset.Parse("2026-08-03");

        CreditMeter.EstimateForRange(day, day).Should().Be(1f);
    }

    [Fact]
    public void EstimateForRange_OneYear_MatchesCeilingOfDailyRate()
    {
        var start = DateTimeOffset.Parse("2025-01-01");
        var end = DateTimeOffset.Parse("2025-12-31");

        // 365 calendar days × 0.35 = 127.75 → 128
        CreditMeter.EstimateForRange(start, end).Should().Be(128f);
    }

    [Fact]
    public void EstimateForRange_TypicalMedianRange_IsAffordableOnFreeTier()
    {
        // Median observed backtest range is ~216 calendar days (plan 16 phase 0);
        // the Free monthly grant (100) must cover its pre-flight estimate.
        var start = DateTimeOffset.Parse("2026-01-01");
        var end = start.AddDays(215);

        CreditMeter.EstimateForRange(start, end).Should().BeLessThanOrEqualTo(100f);
    }
}
