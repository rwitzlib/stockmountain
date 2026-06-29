using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Infrastructure.Config;

[ExcludeFromCodeCoverage]
public class MarketDataConfig
{
    public string TableName { get; set; }
    public string S3BucketName { get; set; }
    public string OrchestratorLambdaName { get; set; }
}
