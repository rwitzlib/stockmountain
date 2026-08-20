using Alpaca.Client.Interfaces;
using Alpaca.Client.Models;
using Microsoft.Extensions.Logging;

namespace Alpaca.Client;

/// <summary>
/// Authoritative market-hours source backed by Alpaca's calendar API (holidays and
/// half-days included). The calendar is fetched once per day and cached, so per-tick
/// checks cost no API calls.
/// </summary>
public class MarketCalendarService(IAlpacaTradingClient tradingClient, ILogger<MarketCalendarService> logger)
{
    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private Dictionary<string, AlpacaCalendarDay> _calendarByDate = [];
    private DateOnly _lastRefreshDate;

    /// <summary>
    /// True while inside today's regular trading session. An optional asOf evaluates the
    /// gate at a different clock — consumers on a delayed data plan pass their data time so
    /// the session covers the data's hours, not the wall clock's.
    /// </summary>
    public async Task<bool> IsMarketOpen(DateTimeOffset? asOf = null)
    {
        var session = await GetTodaySession();
        if (session is null)
        {
            return false;
        }

        var now = asOf ?? DateTimeOffset.UtcNow;
        return now >= session.Value.Open && now < session.Value.Close;
    }

    /// <summary>
    /// Today's regular trading hours in UTC, or null when the market is closed all day.
    /// </summary>
    public async Task<(DateTimeOffset Open, DateTimeOffset Close)?> GetTodaySession()
    {
        var easternNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Eastern);
        var today = DateOnly.FromDateTime(easternNow.DateTime);

        await EnsureCalendar(today);

        if (_calendarByDate.Count == 0)
        {
            // Calendar unavailable and no cache: fail open on weekdays with standard hours so a
            // transient Alpaca outage cannot silently stop trading. Holidays slip through this
            // fallback, but downstream execution checks still gate real orders.
            if (easternNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                return null;
            }

            logger.LogWarning("Market calendar unavailable; assuming standard 09:30-16:00 ET session.");
            return (ToUtc(today, new TimeOnly(9, 30)), ToUtc(today, new TimeOnly(16, 0)));
        }

        if (!_calendarByDate.TryGetValue(today.ToString("yyyy-MM-dd"), out var day))
        {
            return null;
        }

        return (ToUtc(today, TimeOnly.Parse(day.Open)), ToUtc(today, TimeOnly.Parse(day.Close)));
    }

    /// <summary>
    /// Opening time (UTC) of the first session that starts after <paramref name="after"/>.
    /// Holiday-aware via the cached calendar; falls back to the next weekday at 09:30 ET
    /// when the calendar is unavailable, mirroring GetTodaySession's fail-open.
    /// </summary>
    public async Task<DateTimeOffset> GetNextSessionOpen(DateTimeOffset after)
    {
        var easternNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Eastern);
        var today = DateOnly.FromDateTime(easternNow.DateTime);

        await EnsureCalendar(today);

        foreach (var entry in _calendarByDate.OrderBy(kvp => kvp.Key, StringComparer.Ordinal))
        {
            var open = ToUtc(DateOnly.Parse(entry.Key), TimeOnly.Parse(entry.Value.Open));
            if (open > after)
            {
                return open;
            }
        }

        var candidate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(after, Eastern).DateTime).AddDays(1);
        while (candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            candidate = candidate.AddDays(1);
        }

        return ToUtc(candidate, new TimeOnly(9, 30));
    }

    private async Task EnsureCalendar(DateOnly today)
    {
        if (_lastRefreshDate == today && _calendarByDate.Count > 0)
        {
            return;
        }

        await _refreshLock.WaitAsync();
        try
        {
            if (_lastRefreshDate == today && _calendarByDate.Count > 0)
            {
                return;
            }

            var days = await tradingClient.GetCalendar(today, today.AddDays(7));

            if (days is { Count: > 0 })
            {
                _calendarByDate = days.ToDictionary(d => d.Date);
                _lastRefreshDate = today;
                logger.LogInformation("Refreshed market calendar: {Count} trading days from {Start}", days.Count, days[0].Date);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh market calendar from Alpaca.");
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static DateTimeOffset ToUtc(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Eastern.GetUtcOffset(local)).ToUniversalTime();
    }
}
