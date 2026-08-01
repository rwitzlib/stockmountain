using MarketViewer.Contracts.Enums;

namespace MarketViewer.Contracts.Models;

public static class TimeframeExtensions
{
    /// <summary>
    /// Converts a Timeframe to a wall-clock duration. Calendar units use the same fixed
    /// approximations as live trading cooldowns (30-day months, 365-day years) so backtest
    /// and Optimus agree on when a cooldown expires.
    /// </summary>
    public static TimeSpan ToTimeSpan(this Timeframe timeframe)
    {
        if (timeframe is null)
        {
            return TimeSpan.Zero;
        }

        return timeframe.Timespan switch
        {
            Timespan.minute => TimeSpan.FromMinutes(timeframe.Multiplier),
            Timespan.hour => TimeSpan.FromHours(timeframe.Multiplier),
            Timespan.day => TimeSpan.FromDays(timeframe.Multiplier),
            Timespan.week => TimeSpan.FromDays(timeframe.Multiplier * 7),
            Timespan.month => TimeSpan.FromDays(timeframe.Multiplier * 30),
            Timespan.quarter => TimeSpan.FromDays(timeframe.Multiplier * 90),
            Timespan.year => TimeSpan.FromDays(timeframe.Multiplier * 365),
            _ => TimeSpan.Zero
        };
    }
}
