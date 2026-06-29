namespace Optimus.Infrastructure.Config;

public class ExecutionDedupConfig
{
    public string TableName { get; set; }
    public int TtlDays { get; set; } = 7;
}
