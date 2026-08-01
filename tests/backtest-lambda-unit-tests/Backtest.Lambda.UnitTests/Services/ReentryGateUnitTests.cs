using Backtest.Lambda.Services;
using FluentAssertions;

namespace Backtest.Lambda.UnitTests.Services;

public class ReentryGateUnitTests
{
    private static readonly DateTimeOffset MarketOpen = DateTimeOffset.Parse("2025-05-27T09:30:00-04:00");

    private static DateTimeOffset At(int minutes) => MarketOpen.AddMinutes(minutes);

    [Fact]
    public void FirstSignal_IsAlwaysEligible()
    {
        var gate = new ReentryGate(allowSimultaneous: false, cooldown: TimeSpan.FromMinutes(15));

        gate.IsEligible("hold", At(0)).Should().BeTrue();
    }

    [Fact]
    public void OpenPosition_BlocksReentry_WhenNotAllowSimultaneous()
    {
        var gate = new ReentryGate(allowSimultaneous: false, cooldown: TimeSpan.Zero);

        gate.IsEligible("hold", At(0)).Should().BeTrue();
        gate.RecordFill("hold", soldAt: At(20));

        gate.IsEligible("hold", At(10)).Should().BeFalse();
    }

    [Fact]
    public void Cooldown_BlocksReentry_UntilExpiry()
    {
        var gate = new ReentryGate(allowSimultaneous: false, cooldown: TimeSpan.FromMinutes(15));

        gate.IsEligible("hold", At(0)).Should().BeTrue();
        gate.RecordFill("hold", soldAt: At(20));

        gate.IsEligible("hold", At(25)).Should().BeFalse("cooldown runs until 20 + 15 = minute 35");
        gate.IsEligible("hold", At(35)).Should().BeTrue();
    }

    [Fact]
    public void ZeroCooldown_AllowsReentry_AtTheCloseMinute()
    {
        var gate = new ReentryGate(allowSimultaneous: false, cooldown: TimeSpan.Zero);

        gate.IsEligible("hold", At(0)).Should().BeTrue();
        gate.RecordFill("hold", soldAt: At(20));

        gate.IsEligible("hold", At(20)).Should().BeTrue("the simulator sells before it buys within a minute");
    }

    [Fact]
    public void ExitTypes_TrackIndependentTimelines()
    {
        var gate = new ReentryGate(allowSimultaneous: false, cooldown: TimeSpan.Zero);

        gate.IsEligible("hold", At(0)).Should().BeTrue();
        gate.IsEligible("high", At(0)).Should().BeTrue();
        gate.RecordFill("hold", soldAt: At(30));
        gate.RecordFill("high", soldAt: At(5));

        gate.IsEligible("hold", At(10)).Should().BeFalse("hold position is still open");
        gate.IsEligible("high", At(10)).Should().BeTrue("high position closed at minute 5");
    }

    [Fact]
    public void AllowSimultaneous_PermitsStackingOpenPositions()
    {
        var gate = new ReentryGate(allowSimultaneous: true, cooldown: TimeSpan.FromMinutes(15));

        gate.IsEligible("hold", At(0)).Should().BeTrue();
        gate.RecordFill("hold", soldAt: At(60));

        gate.IsEligible("hold", At(10)).Should().BeTrue("no position has closed yet, so no cooldown is running");
    }

    [Fact]
    public void AllowSimultaneous_CooldownStillStartsAtEachClose()
    {
        var gate = new ReentryGate(allowSimultaneous: true, cooldown: TimeSpan.FromMinutes(15));

        gate.IsEligible("hold", At(0)).Should().BeTrue();
        gate.RecordFill("hold", soldAt: At(10));
        gate.RecordFill("hold", soldAt: At(60));

        gate.IsEligible("hold", At(12)).Should().BeFalse("first close at minute 10 starts a cooldown until 25");
        gate.IsEligible("hold", At(30)).Should().BeTrue();
        gate.IsEligible("hold", At(70)).Should().BeFalse("second close at minute 60 starts a cooldown until 75");
        gate.IsEligible("hold", At(80)).Should().BeTrue();
    }

    [Fact]
    public void LatestClose_DrivesCooldown_WhenSeveralMature()
    {
        var gate = new ReentryGate(allowSimultaneous: true, cooldown: TimeSpan.FromMinutes(15));

        // Later-opened positions can close earlier than earlier-opened ones.
        gate.RecordFill("hold", soldAt: At(40));
        gate.RecordFill("hold", soldAt: At(25));

        gate.IsEligible("hold", At(50)).Should().BeFalse("latest close (40) runs a cooldown until 55");
        gate.IsEligible("hold", At(55)).Should().BeTrue();
    }
}
