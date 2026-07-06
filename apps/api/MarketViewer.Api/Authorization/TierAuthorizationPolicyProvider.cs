using MarketViewer.Contracts.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace MarketViewer.Api.Authorization;

public class TierAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(RequiresTierAttribute.PolicyPrefix))
        {
            var tierName = policyName.Substring(RequiresTierAttribute.PolicyPrefix.Length);
            if (Enum.TryParse<UserRole>(tierName, out var tier))
            {
                return new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new TierRequirement(tier))
                    .Build();
            }
        }

        if (policyName == RequiresAdminAttribute.PolicyName)
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new AdminRequirement())
                .Build();
        }

        return await base.GetPolicyAsync(policyName);
    }
}
