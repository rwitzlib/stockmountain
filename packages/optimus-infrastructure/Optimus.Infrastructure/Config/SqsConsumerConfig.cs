namespace Optimus.Infrastructure.Config;

public class SqsConsumerConfig
{
    public string QueueUrl { get; set; }
    public int MaxNumberOfMessages { get; set; } = 10;
    public int WaitTimeSeconds { get; set; } = 20;
    public int VisibilityTimeoutSeconds { get; set; } = 60;
    public bool Enabled { get; set; } = false;
}
