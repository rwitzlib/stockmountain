using Optimus.Infrastructure.Config;

namespace Optimus.Infrastructure.Services;

/// <summary>
/// The clock the market data actually runs on: wall clock minus the data plan's
/// delay. Anything that pairs a time with a delayed price or bar — trade
/// timestamps, exit evaluation, the session-close gate — must use this clock so
/// records match backtests and no code changes are needed when the feed goes
/// live (DelayMinutes = 0 makes this the wall clock).
/// </summary>
public class MarketDataClock(MarketDataConfig config)
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow.AddMinutes(-config.DelayMinutes);
}
