using MarketViewer.Contracts.Enums;
using MarketViewer.Core.Auth;
using Microsoft.AspNetCore.Authorization;

namespace MarketViewer.Api.Authorization;

public class TierRequirement(UserRole minimumTier) : IAuthorizationRequirement
{
    public UserRole MinimumTier { get; } = minimumTier;
}

public class AdminRequirement : IAuthorizationRequirement;

/// <summary>
/// Authorizes against the AuthContext populated by AuthContextMiddleware from the
/// user's DynamoDB record — the database is the source of truth for roles, not token claims.
/// </summary>
public class TierAuthorizationHandler(AuthContext authContext) : AuthorizationHandler<TierRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TierRequirement requirement)
    {
        if (authContext.HasTier(requirement.MinimumTier))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public class AdminAuthorizationHandler(AuthContext authContext) : AuthorizationHandler<AdminRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminRequirement requirement)
    {
        if (authContext.IsAuthenticated && authContext.IsAdmin)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
