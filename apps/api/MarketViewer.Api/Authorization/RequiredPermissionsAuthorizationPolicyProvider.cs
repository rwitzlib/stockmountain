using MarketViewer.Contracts.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace MarketViewer.Api.Authorization;

public class RequiredPermissionsAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    private readonly AuthorizationOptions _options = options.Value;

    public override async Task<AuthorizationPolicy> GetPolicyAsync(string policyName)
    {
        // Check if this is our policy
        if (policyName.StartsWith("RequiredPermissions:"))
        {
            // Return policy from cache if it exists
            if (_options.GetPolicy(policyName) != null)
            {
                return await base.GetPolicyAsync(policyName);
            }

            // Extract the roles from the policy name
            var rolesPart = policyName.Substring("RequiredPermissions:".Length);
            var roleNames = rolesPart.Split(',', StringSplitOptions.RemoveEmptyEntries);

            var roles = new List<UserRole>();
            foreach (var roleName in roleNames)
            {
                if (Enum.TryParse<UserRole>(roleName, out var role))
                {
                    roles.Add(role);
                }
            }

            // Create a policy with the required roles
            var policyBuilder = new AuthorizationPolicyBuilder();
            policyBuilder.AddRequirements(new RequiredPermissionsRequirement(roles.ToArray()));
            policyBuilder.RequireAuthenticatedUser();

            return policyBuilder.Build();
        }

        // Get the policy from the base provider
        return await base.GetPolicyAsync(policyName);
    }
}