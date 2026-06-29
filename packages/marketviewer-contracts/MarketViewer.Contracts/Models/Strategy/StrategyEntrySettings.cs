using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MarketViewer.Contracts.Models.Strategy;

[ExcludeFromCodeCoverage]
public class StrategyEntrySettings
{
    public List<string> Filters { get; set; }

    public string ComputeStrategyHash()
    {
        var serialized = JsonSerializer.Serialize(this);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(serialized));
        return Convert.ToBase64String(hashBytes);
    }
}
