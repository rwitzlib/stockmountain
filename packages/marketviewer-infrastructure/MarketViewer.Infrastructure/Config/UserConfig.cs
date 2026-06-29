using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Infrastructure.Config;

[ExcludeFromCodeCoverage]
public class UserConfig
{
    public string TableName { get; set; }
}
