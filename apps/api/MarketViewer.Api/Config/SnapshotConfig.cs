namespace MarketViewer.Api.Config;

public class SnapshotConfig
{
    /// <summary>
    /// Max single-ticker probe requests per run while waiting for the provider to
    /// flush the latest completed minute bar.
    /// </summary>
    public int ProbeMaxAttempts { get; set; } = 5;

    /// <summary>Delay between probe attempts.</summary>
    public int ProbeDelayMs { get; set; } = 400;

    /// <summary>
    /// Max tickers per run that fall back to REST aggregates for gap backfill.
    /// Bounds the request stampede after several consecutive failed polls.
    /// </summary>
    public int BackfillMaxTickers { get; set; } = 50;

    /// <summary>
    /// Gaps longer than this are assumed to be natural illiquidity (the ticker just
    /// didn't trade) and skip the REST fallback. A missed snapshot poll produces
    /// 1-2 minute gaps across many tickers at once; a thin ticker resuming after a
    /// quiet half hour produces one long gap that REST can't fill anyway.
    /// </summary>
    public int RestBackfillMaxGapMinutes { get; set; } = 5;
}
