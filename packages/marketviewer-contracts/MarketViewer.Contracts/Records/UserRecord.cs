using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Enums.Strategy;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Records;

[ExcludeFromCodeCoverage]
public class UserRecord
{
    public string Id { get; set; }
    public string AvatarUrl { get; set; }
    public float Credits { get; set; }
    public bool IsPublic { get; set; }
    public UserRole Role { get; set; }
    public bool IsAdmin { get; set; }
    public Dictionary<IntegrationType, string> Tokens { get; set; }
}
