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
}
