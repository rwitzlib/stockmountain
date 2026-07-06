using MarketViewer.Contracts.Enums;

namespace MarketViewer.Core.Auth;

public static class AuthContextExtensions
{
    /// <summary>
    /// True when the user meets the minimum tier. Admins satisfy every tier requirement.
    /// </summary>
    public static bool HasTier(this AuthContext authContext, UserRole minimumTier)
    {
        if (!authContext.IsAuthenticated)
        {
            return false;
        }

        return authContext.IsAdmin || (authContext.Role.HasValue && authContext.Role.Value >= minimumTier);
    }
}
