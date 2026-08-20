using FluentAssertions;
using MarketViewer.Api.Jobs;
using Xunit;

namespace MarketViewer.Api.UnitTests.Jobs;

public class ScannerJobUnitTests
{
    [Fact]
    public void ComputeWindow_CompletedBarMode_IsStableWithinTheDataMinute()
    {
        // All four 15-second scan ticks inside one data-clock minute must produce
        // the same window, so the executor dedupes re-signals of the same bar.
        var minuteStart = DateTimeOffset.Parse("2026-07-20T10:32:00-04:00");

        var windows = new[] { 0, 15, 30, 45 }
            .Select(seconds => ScannerJob.ComputeWindow(minuteStart.AddSeconds(seconds), completedBarEntries: true))
            .Distinct()
            .ToList();

        windows.Should().HaveCount(1);
        windows[0].Should().Be(minuteStart.ToUnixTimeSeconds());
    }

    [Fact]
    public void ComputeWindow_CompletedBarMode_AdvancesWithTheDataMinute()
    {
        var minuteStart = DateTimeOffset.Parse("2026-07-20T10:32:00-04:00");

        var first = ScannerJob.ComputeWindow(minuteStart.AddSeconds(45), completedBarEntries: true);
        var second = ScannerJob.ComputeWindow(minuteStart.AddSeconds(60), completedBarEntries: true);

        second.Should().Be(first + 60);
    }

    [Theory]
    [InlineData("2026-07-20T15:58:45-04:00", true)]  // last scannable tick before the final minute
    [InlineData("2026-07-20T15:59:00-04:00", false)] // final minute: a fill would land at 16:00 with no bar behind it
    [InlineData("2026-07-20T15:59:45-04:00", false)]
    public void HasFillBarRemaining_StopsEntriesInTheFinalSessionMinute(string dataTime, bool expected)
    {
        var sessionClose = DateTimeOffset.Parse("2026-07-20T16:00:00-04:00");

        ScannerJob.HasFillBarRemaining(DateTimeOffset.Parse(dataTime), sessionClose).Should().Be(expected);
    }

    [Fact]
    public void HasFillBarRemaining_UsesTheActualSessionClose_OnHalfDays()
    {
        var earlyClose = DateTimeOffset.Parse("2026-11-27T13:00:00-05:00");

        ScannerJob.HasFillBarRemaining(DateTimeOffset.Parse("2026-11-27T12:58:00-05:00"), earlyClose).Should().BeTrue();
        ScannerJob.HasFillBarRemaining(DateTimeOffset.Parse("2026-11-27T12:59:30-05:00"), earlyClose).Should().BeFalse();
    }
}
