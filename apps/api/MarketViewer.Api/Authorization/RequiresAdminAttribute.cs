using Microsoft.AspNetCore.Authorization;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Api.Authorization;

/// <summary>
/// Requires the caller's user record to have the admin flag. Purchase tiers never satisfy this.
/// </summary>
[ExcludeFromCodeCoverage]
public class RequiresAdminAttribute : AuthorizeAttribute
{
    public const string PolicyName = "RequiresAdmin";

    public RequiresAdminAttribute()
    {
        Policy = PolicyName;
    }
}
