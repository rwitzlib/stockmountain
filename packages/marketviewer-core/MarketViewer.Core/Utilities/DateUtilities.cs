using System;
using System.Collections.Generic;

namespace MarketViewer.Core.Utilities;

public static class DateUtilities
{
    public static List<DateTime> GetMarketOpenDays(DateTimeOffset start, DateTimeOffset end)
    {
        var dates = new List<DateTime>();

        var days = (end.Date - start.Date).Days;

        for (int i = 0; i <= days; i++)
        {
            var currentDay = start.AddDays(i).Date;

            if (currentDay.DayOfWeek != DayOfWeek.Saturday && currentDay.DayOfWeek != DayOfWeek.Sunday)
            {
                dates.Add(currentDay);
            }
        }

        return dates;
    }

    public static DateTimeOffset EndOfDay(this DateTimeOffset date)
    {
        return new DateTimeOffset(date.Year, date.Month, date.Day, 23, 59, 0, date.Offset);
    }

    public static DateTimeOffset StartOfDay(this DateTimeOffset date)
    {
        return new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, date.Offset);
    }
}
