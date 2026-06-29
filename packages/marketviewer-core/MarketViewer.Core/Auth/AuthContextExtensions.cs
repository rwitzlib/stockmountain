using MarketViewer.Contracts.Enums;

namespace MarketViewer.Core.Auth;

public static class AuthContextExtensions
{
    public static bool HasRole(this AuthContext authContext, UserRole role)
    {
        return authContext.IsAuthenticated && authContext.Role == role;
    }

    public static bool HasAnyRole(this AuthContext authContext, params UserRole[] roles)
    {
        return authContext.IsAuthenticated && authContext.Role.HasValue && roles.Contains(authContext.Role.Value);
    }

    public static bool IsAdmin(this AuthContext authContext)
    {
        return authContext.HasRole(UserRole.Admin);
    }

    public static bool IsPremiumOrAbove(this AuthContext authContext)
    {
        return authContext.HasAnyRole(UserRole.Premium, UserRole.Admin);
    }
}