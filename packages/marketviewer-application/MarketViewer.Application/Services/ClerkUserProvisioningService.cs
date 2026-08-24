using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Records;
using MarketViewer.Core.Services;
using Microsoft.Extensions.Logging;

namespace MarketViewer.Application.Services;

public class ClerkUserProvisioningService(
    IUserRepository userRepository,
    ILogger<ClerkUserProvisioningService> logger)
{
    /// <summary>Free-tier monthly grant (plan 16 phase 0); issued at signup and on each monthly refill.</summary>
    private const float StartingCredits = 100;

    public async Task<bool> Provision(ClerkUserProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            logger.LogWarning("Cannot provision Clerk user profile without a Clerk user ID");
            return false;
        }

        var user = new UserRecord
        {
            Id = profile.Id,
            AvatarUrl = profile.AvatarUrl ?? string.Empty,
            Credits = StartingCredits,
            MaxCredits = StartingCredits,
            PurchasedCredits = 0,
            IsPublic = false,
            Role = UserRole.Free,
            IsAdmin = false,
            Tokens = []
        };

        return await userRepository.Provision(user);
    }
}
