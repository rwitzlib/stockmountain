using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;

namespace Optimus.Infrastructure.Utilities;

public static class DateUtilities
{
    public static DateTimeOffset GetEndDate(DateTimeOffset start, Timeframe timeframe)
    {
        var totalDays = timeframe.Timespan switch
        {
            Timespan.minute => timeframe.Multiplier / 1440,
            Timespan.hour => timeframe.Multiplier / 24,
            Timespan.day => timeframe.Multiplier,
            Timespan.week => timeframe.Multiplier * 7,
            Timespan.month => throw new NotImplementedException(),
            Timespan.quarter => throw new NotImplementedException(),
            Timespan.year => throw new NotImplementedException(),
            _ => throw new NotImplementedException()
        };

        var weekends = ((int)start.DayOfWeek + totalDays) / 5 * 2;

        var businessDays = timeframe.Timespan switch
        {
            Timespan.minute => TimeSpan.FromMinutes(timeframe.Multiplier),
            Timespan.hour => TimeSpan.FromHours(timeframe.Multiplier),
            Timespan.day => TimeSpan.FromDays(timeframe.Multiplier),
            Timespan.week => TimeSpan.FromDays(timeframe.Multiplier * 7),
            Timespan.month => throw new NotImplementedException(),
            Timespan.quarter => throw new NotImplementedException(),
            Timespan.year => throw new NotImplementedException(),
            _ => throw new NotImplementedException()
        };

        var end = start.Add(businessDays).AddDays(weekends);

        if (end.DayOfWeek is DayOfWeek.Saturday)
        {
            end = end.AddDays(-1);
        }
        if (end.DayOfWeek is DayOfWeek.Sunday)
        {
            end = end.AddDays(-2);
        }

        return end;
    }
}
