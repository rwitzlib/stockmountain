using MarketViewer.Contracts.Enums;
using MarketViewer.Core.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Text.Json;

namespace MarketViewer.Api.Authorization;

public class RequiredPermissionsHandler(IHttpContextAccessor httpContextAccessor) : AuthorizationHandler<RequiredPermissionsRequirement>
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RequiredPermissionsRequirement requirement)
    {
        var propertiesClaim = context.User.FindFirstValue("properties");
        if (string.IsNullOrEmpty(propertiesClaim))
        {
            return Task.CompletedTask; // Requirement not satisfied
        }

        var subject = JsonSerializer.Deserialize<Subject>(propertiesClaim, _jsonSerializerOptions);
        if (subject == null)
        {
            return Task.CompletedTask; // Requirement not satisfied
        }

        if (requirement.RequiredRoles.Any(role => role == subject.Role))
        {
            context.Succeed(requirement);

            // Add UserId to HttpContext items
            if (httpContextAccessor.HttpContext != null)
            {
                httpContextAccessor.HttpContext.Items["UserId"] = subject.Username;
            }
        }

        return Task.CompletedTask;
    }
}

public class RequiredPermissionsRequirement(UserRole[] requiredRoles) : IAuthorizationRequirement
{
    public UserRole[] RequiredRoles { get; } = requiredRoles;
}