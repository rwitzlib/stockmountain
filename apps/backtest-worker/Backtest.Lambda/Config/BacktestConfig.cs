using System.Diagnostics.CodeAnalysis;

namespace Backtest.Lambda.Config;

[ExcludeFromCodeCoverage]
public class BacktestConfig
{
    public string TableName { get; set; }
    public string RequestDetailsIndexName { get; set; }
    public string LambdaName { get; set; }
    public string S3BucketName { get; set; }
}
