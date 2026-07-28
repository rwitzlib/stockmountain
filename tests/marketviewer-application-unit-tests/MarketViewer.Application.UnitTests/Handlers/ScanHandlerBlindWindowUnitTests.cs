using FluentAssertions;
using MarketViewer.Application.Handlers.Market.Scan;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Responses.Market;
using Massive.Client.Models;
using Xunit;

namespace MarketViewer.Application.UnitTests.Handlers;

/// <summary>
/// Covers the scan blind window: between a minute closing and the snapshot poll
/// appending it, the just-closed bar only exists in the websocket ring buffer, and
/// TryAddBarToResponse must splice it in ahead of the in-progress live bar.
/// </summary>
public class ScanHandlerBlindWindowUnitTests
{
    private static readonly long MinuteMs = 60_000;
    private static readonly long BaseTs = DateTimeOffset.Parse("2026-07-20T10:00:00-04:00").ToUnixTimeMilliseconds();

    [Fact]
    public void MinuteBranch_AppendsRingBarBetweenHistoryAndLiveBar()
    {
        // History ends at minute M-1; ring holds completed M; live bar is in-progress M+1.
        var response = ResponseEndingAt(BaseTs);
        var ringBar = BarAt(BaseTs + MinuteMs);
        var liveBar = BarAt(BaseTs + 2 * MinuteMs);

        ScanHandler.TryAddBarToResponse(1, Timespan.minute, liveBar, [ringBar], response);

        response.Results.Should().HaveCount(4);
        response.Results[^2].Timestamp.Should().Be(ringBar.Timestamp);
        response.Results[^1].Timestamp.Should().Be(liveBar.Timestamp);
        response.Results.Should().BeInAscendingOrder(bar => bar.Timestamp);
    }

    [Fact]
    public void MinuteBranch_DoesNotDuplicate_OnceSnapshotBarLanded()
    {
        // Snapshot already appended M to history; the ring still holds M.
        var response = ResponseEndingAt(BaseTs + MinuteMs);
        var ringBar = BarAt(BaseTs + MinuteMs);
        var liveBar = BarAt(BaseTs + 2 * MinuteMs);

        ScanHandler.TryAddBarToResponse(1, Timespan.minute, liveBar, [ringBar], response);

        response.Results.Should().HaveCount(3);
        response.Results[^1].Timestamp.Should().Be(liveBar.Timestamp);
        response.Results.Should().OnlyHaveUniqueItems(bar => bar.Timestamp);
    }

    [Fact]
    public void MinuteBranch_AppendsRingBar_WhenLiveBarMissing()
    {
        // Feed rolled over but no tick has arrived for the new minute yet (or the
        // live bar entry expired): the ring bar alone must still be appended.
        var response = ResponseEndingAt(BaseTs);
        var ringBar = BarAt(BaseTs + MinuteMs);

        ScanHandler.TryAddBarToResponse(1, Timespan.minute, null, [ringBar], response);

        response.Results.Should().HaveCount(3);
        response.Results[^1].Timestamp.Should().Be(ringBar.Timestamp);
    }

    [Fact]
    public void MinuteBranch_SkipsRingBar_NotNewerThanLiveBar()
    {
        // Ring bar and live bar are the same minute (rollover race): live bar wins.
        var response = ResponseEndingAt(BaseTs);
        var ringBar = BarAt(BaseTs + MinuteMs);
        var liveBar = BarAt(BaseTs + MinuteMs);

        ScanHandler.TryAddBarToResponse(1, Timespan.minute, liveBar, [ringBar], response);

        response.Results.Should().HaveCount(3);
        response.Results[^1].Should().BeSameAs(liveBar);
    }

    [Fact]
    public void MinuteBranch_MultipleRingBars_FillMultipleMissedMinutes()
    {
        // Two snapshot polls missed while the websocket stayed alive.
        var response = ResponseEndingAt(BaseTs);
        var ringBars = new[] { BarAt(BaseTs + MinuteMs), BarAt(BaseTs + 2 * MinuteMs) };
        var liveBar = BarAt(BaseTs + 3 * MinuteMs);

        ScanHandler.TryAddBarToResponse(1, Timespan.minute, liveBar, ringBars, response);

        response.Results.Should().HaveCount(5);
        response.Results.Should().BeInAscendingOrder(bar => bar.Timestamp);
    }

    [Fact]
    public void MinuteBranch_IgnoresNonUnitMultiplier()
    {
        var response = ResponseEndingAt(BaseTs);

        ScanHandler.TryAddBarToResponse(5, Timespan.minute, BarAt(BaseTs + 2 * MinuteMs), [BarAt(BaseTs + MinuteMs)], response);

        response.Results.Should().HaveCount(2);
    }

    [Fact]
    public void HourBranch_StillMergesLiveBarOnly()
    {
        var hourStart = DateTimeOffset.Parse("2026-07-20T10:00:00-04:00").ToUnixTimeMilliseconds();
        var response = ResponseEndingAt(hourStart);
        var liveBar = BarAt(hourStart + 5 * MinuteMs);
        liveBar.High = 100f;

        ScanHandler.TryAddBarToResponse(1, Timespan.hour, liveBar, [BarAt(hourStart + MinuteMs)], response);

        // Same hour: merged into the last bar, no append, ring ignored.
        response.Results.Should().HaveCount(2);
        response.Results[^1].High.Should().Be(100f);
    }

    private static StocksResponse ResponseEndingAt(long lastTimestamp)
    {
        return new StocksResponse
        {
            Ticker = "SPY",
            Results = [BarAt(lastTimestamp - MinuteMs), BarAt(lastTimestamp)]
        };
    }

    private static Bar BarAt(long timestamp)
    {
        return new Bar
        {
            Timestamp = timestamp,
            Open = 10f,
            Close = 10f,
            High = 11f,
            Low = 9f,
            Volume = 100f,
            Vwap = 10f
        };
    }
}
