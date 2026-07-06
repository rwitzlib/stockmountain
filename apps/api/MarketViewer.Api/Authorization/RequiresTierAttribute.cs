using MarketViewer.Contracts.Enums;
using Microsoft.AspNetCore.Authorization;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Api.Authorization;

/// <summary>
/// Requires the caller to hold at least the given purchase tier. Admins always pass.
/// </summary>
[ExcludeFromCodeCoverage]
public class RequiresTierAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "RequiresTier:";

    public RequiresTierAttribute(UserRole minimumTier)
    {
        Policy = $"{PolicyPrefix}{minimumTier}";
    }
}
