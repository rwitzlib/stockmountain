using MarketViewer.Contracts.Enums;

namespace MarketViewer.Contracts.MarketData;

public static class MarketDataWorkPlanner
{
    /// <summary>
    /// Expands a date range into the aggregator work items required to cover it.
    /// Minute aggregates are stored per day, so every trading day needs a run.
    /// Hour and day aggregates are stored as one object per month/year and each
    /// aggregator run fetches from the start of that period, so only the latest
    /// trading day per month/year needs to run. This also prevents concurrent
    /// runs for the same month/year from racing on the same S3 object.
    /// </summary>
    public static IEnumerable<(DateTimeOffset Date, Timespan Timespan)> BuildWorkDates(
        DateTimeOffset start,
        DateTimeOffset end,
        IEnumerable<Timespan> timespans)
    {
        var tradingDays = new List<DateTime>();
        var days = (end.Date - start.Date).Days;

        for (var i = 0; i <= days; i++)
        {
            var date = start.Date.AddDays(i);
            if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            {
                tradingDays.Add(date);
            }
        }

        foreach (var timespan in timespans)
        {
            var dates = timespan switch
            {
                Timespan.hour => tradingDays.GroupBy(date => (date.Year, date.Month)).Select(group => group.Max()),
                Timespan.day => tradingDays.GroupBy(date => date.Year).Select(group => group.Max()),
                _ => tradingDays
            };

            foreach (var date in dates)
            {
                yield return (date, timespan);
            }
        }
    }
}
