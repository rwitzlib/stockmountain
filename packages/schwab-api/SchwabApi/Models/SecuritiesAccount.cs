using System.Diagnostics.CodeAnalysis;

namespace SchwabApi.Models;

[ExcludeFromCodeCoverage]
public class SecuritiesAccount
{
    public string Type { get; set; }
    public string AccountNumber { get; set; }
    public string HashValue { get; set; }
    public int RoundTrips { get; set; }
    public bool IsDayTrader { get; set; }
    public bool IsClosingOnlyRestricted { get; set; }
    public bool PfcbFlag { get; set; }
    public IEnumerable<Position> Positions { get; set; } = [];
    public Balance InitialBalances { get; set; }
    public Balance CurrentBalances { get; set; }
    public Balance ProjectedBalances { get; set; }
}
