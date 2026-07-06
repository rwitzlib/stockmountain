using MarketViewer.Application.Services;
using MarketViewer.Core.Auth;
using MarketViewer.Core.Services;
using System.Security.Claims;

namespace MarketViewer.Api.Middleware;

/// <summary>
/// Resolves the authenticated caller's app profile. The Clerk JWT (already validated by the
/// authentication middleware) only proves identity via its sub claim; role and admin status
/// always come from the DynamoDB user record so subscription changes take effect on the
/// next request and token claims can never grant access.
/// </summary>
public class AuthContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        AuthContext authContext,
        IUserRepository userRepository,
        ClerkUserProvisioningService provisioningService,
        ILogger<AuthContextMiddleware> logger)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirstValue("sub")
                ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(userId))
            {
                var user = await userRepository.Get(userId);

                if (user is null)
                {
                    // The Clerk user.created webhook may not have landed yet (or was dropped).
                    // Provision is idempotent, so racing the webhook is safe.
                    logger.LogInformation("No user record for authenticated user {UserId}; provisioning inline", userId);
                    await provisioningService.Provision(new ClerkUserProfile(userId, null));
                    user = await userRepository.Get(userId);
                }

                if (user is not null)
                {
                    authContext.UserId = user.Id;
                    authContext.Role = user.Role;
                    authContext.IsAdmin = user.IsAdmin;
                    authContext.IsAuthenticated = true;

                    context.Items["UserId"] = user.Id;
                }
                else
                {
                    logger.LogError("Failed to provision user record for authenticated user {UserId}", userId);
                }
            }
        }

        await next(context);
    }
}
