using System.Diagnostics.CodeAnalysis;

namespace SchwabApi.Models;

[ExcludeFromCodeCoverage]
public class Account
{
    public SecuritiesAccount SecuritiesAccount { get; set; }
    public AggregatedBalance AggregatedBalance { get; set; }
}
