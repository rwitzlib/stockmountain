using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Records.Data;

[ExcludeFromCodeCoverage]
public class SecurityMasterRecord
{
    public string Symbol { get; set; }
    public int? Id { get; set; }
    public bool Active { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string Market { get; set; }
    public string Type { get; set; }
}
