using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Infrastructure.Config;

[ExcludeFromCodeCoverage]
public class MarketDataConfig
{
    public string TableName { get; set; }
    public string S3BucketName { get; set; }
    public string OrchestratorLambdaName { get; set; }

    /// <summary>
    /// How far behind real time the Massive data plan delivers data (15 on the
    /// delayed plan, 0 on real-time). Live scans run on the data clock — wall clock
    /// minus this delay — so time-of-day filters and the market-close gate line up
    /// with the bars being evaluated instead of the wall clock.
    /// </summary>
    public int DelayMinutes { get; set; }
}
