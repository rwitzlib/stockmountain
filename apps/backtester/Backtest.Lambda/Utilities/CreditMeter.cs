namespace Backtest.Lambda.Utilities;

/// <summary>
/// Credit metering. 1 credit = 100 GB-seconds of worker compute (plan 16 phase 0), which
/// puts a median backtest around 27 credits and makes the marketed 100/1,000/5,000 monthly
/// tier grants meaningful.
/// </summary>
public static class CreditMeter
{
    public const float GbSecondsPerCredit = 100f;

    /// <summary>
    /// Pre-flight estimate per calendar day of backtested range: ~p90 of observed actual
    /// cost, so ~10% of runs may cost more than estimated — the settlement-time debit
    /// failure path covers those.
    /// </summary>
    public const float EstimatedCreditsPerCalendarDay = 0.35f;

    public static float Compute(float memoryGb, double elapsedSeconds)
    {
        return memoryGb * (float)elapsedSeconds / GbSecondsPerCredit;
    }

    public static float EstimateForRange(DateTimeOffset start, DateTimeOffset end)
    {
        var calendarDays = (end - start).Days + 1;
        return (float)Math.Ceiling(calendarDays * EstimatedCreditsPerCalendarDay);
    }
}
