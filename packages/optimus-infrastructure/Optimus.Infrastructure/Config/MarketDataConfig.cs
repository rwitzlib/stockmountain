using System.Diagnostics.CodeAnalysis;

namespace Optimus.Infrastructure.Config;

[ExcludeFromCodeCoverage]
public class MarketDataConfig
{
    /// <summary>
    /// How far behind real time the Massive data plan delivers data (15 on the
    /// delayed plan, 0 on real-time). Mirrors the MarketViewer API setting of the
    /// same name — flip both to 0 when moving to a live feed. Trade timestamps,
    /// exit evaluation, and the sell-side market-close gate all run on the data
    /// clock (wall clock minus this delay) so they line up with the bars and
    /// prices being evaluated instead of the wall clock.
    /// </summary>
    public int DelayMinutes { get; set; }
}
