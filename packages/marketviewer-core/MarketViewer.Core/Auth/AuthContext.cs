using MarketViewer.Contracts.Enums;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Core.Auth;

[ExcludeFromCodeCoverage]
public class AuthContext
{
    public string UserId { get; set; }
    public UserRole? Role { get; set; }
    public bool IsAuthenticated { get; set; }
}