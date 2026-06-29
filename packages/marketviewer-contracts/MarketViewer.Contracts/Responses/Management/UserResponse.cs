using MarketViewer.Contracts.Enums;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Responses.Management;

[ExcludeFromCodeCoverage]
public class UserResponse
{
    public string Id { get; set; }
    public string AvatarUrl { get; set; }
    public float Credits { get; set; }
    public bool IsPublic { get; set; }
    public UserRole Role { get; set; }
}
