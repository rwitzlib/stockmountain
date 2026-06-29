using MarketViewer.Contracts.Enums;
using MarketViewer.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using System.Text.Json;

namespace MarketViewer.Api.Authorization;

public class MetricsBearerTokenHandler(IHttpContextAccessor httpContextAccessor) : AuthorizationHandler<MetricsBearerTokenRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MetricsBearerTokenRequirement requirement)
    {
        //// Get the Authorization header
        var authHeader = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask; // Requirement not satisfied
        }

        // Extract the token
        var token = authHeader.Substring("Bearer ".Length).Trim();

        // Get the expected dummy token from configuration
        var expectedToken = "asdf";

        // Check if the token matches
        if (token == expectedToken)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public class MetricsBearerTokenRequirement : IAuthorizationRequirement
{
}