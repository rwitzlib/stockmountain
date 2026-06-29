using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Infrastructure.Config;

[ExcludeFromCodeCoverage]
public class BacktestConfig
{
    public string TableName { get; set; }
    public string RequestDetailsIndexName { get; set; }
    public string UserIndexName { get; set; }
    public string LambdaName { get; set; }
    public string S3BucketName { get; set; }
}
