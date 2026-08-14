using Microsoft.AspNetCore.Authorization;
using System.Security.Cryptography;
using System.Text;

namespace MarketViewer.Api.Authorization;

/// <summary>
/// Shared-secret bearer auth for service-to-service endpoints (e.g. Optimus polling
/// live prices). The expected token comes from InternalAuth:Token; when it is unset
/// the requirement never succeeds, so the endpoint fails closed.
/// </summary>
public class InternalTokenHandler(IHttpContextAccessor httpContextAccessor, IConfiguration configuration) : AuthorizationHandler<InternalTokenRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, InternalTokenRequirement requirement)
    {
        var authHeader = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        var expectedToken = configuration["InternalAuth:Token"];

        if (!string.IsNullOrEmpty(expectedToken)
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(expectedToken)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public class InternalTokenRequirement : IAuthorizationRequirement
{
    public const string PolicyName = "InternalToken";
}
