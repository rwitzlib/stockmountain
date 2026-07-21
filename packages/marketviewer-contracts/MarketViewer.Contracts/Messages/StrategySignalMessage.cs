using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Messages;

/// <summary>
/// Entry signal published directly to SQS when a strategy's entry filters match.
/// This message is the hot-path transport; the DynamoDB scan record is an async audit artifact.
/// </summary>
[ExcludeFromCodeCoverage]
public class StrategySignalMessage
{
    public string StrategyHash { get; set; }
    public long Window { get; set; }
    public List<string> Tickers { get; set; }
    public long SignalAtUnixMs { get; set; }
}
