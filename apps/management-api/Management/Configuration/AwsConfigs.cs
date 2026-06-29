using System.Diagnostics.CodeAnalysis;

namespace Management.Configuration;

[ExcludeFromCodeCoverage]
public class AwsConfigs
{
    public string DeployTableName { get; set; }
    public string DeployTokenName { get; set; }
}
