using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;

namespace Backtest.Lambda.Utilities;

public static class DateUtilities
{
    public static DateTimeOffset ToTimezone(this DateTimeOffset date, TimeZoneInfo timezone)
    {    
        var offset = timezone.IsDaylightSavingTime(date) ? TimeSpan.FromHours(-4) : TimeSpan.FromHours(-5);

        return date.ToOffset(offset);
    }

    public static (DateTimeOffset Start, DateTimeOffset End)? GetTimeframeRange(DateTimeOffset timestamp, Timeframe timeframe)
    {
        TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var offset = timeZone.IsDaylightSavingTime(timestamp) ? TimeSpan.FromHours(-4) : TimeSpan.FromHours(-5);

        DateTimeOffset windowStart = new DateTimeOffset(timestamp.Year, timestamp.Month, timestamp.Day, 4, 0, 0, offset);
        DateTimeOffset marketClose = new DateTimeOffset(timestamp.Year, timestamp.Month, timestamp.Day, 16, 0, 0, offset);

        while (windowStart < marketClose)
        {
            var windowEnd = timeframe.Timespan switch
            {
                Timespan.minute => windowStart.AddMinutes(timeframe.Multiplier),
                Timespan.hour => windowStart.AddHours(timeframe.Multiplier),
                Timespan.day => windowStart.AddDays(timeframe.Multiplier),
                _ => throw new NotImplementedException()
            };

            if (windowEnd > timestamp)
            {
                return (windowStart, windowEnd);
            }

            windowStart = windowEnd;
        }

        return null;
    }
}
