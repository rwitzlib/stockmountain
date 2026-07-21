using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Api.Config;

[ExcludeFromCodeCoverage]
public class SignalQueueConfig
{
    public string QueueUrl { get; set; }
    public bool Enabled { get; set; } = false;
}
