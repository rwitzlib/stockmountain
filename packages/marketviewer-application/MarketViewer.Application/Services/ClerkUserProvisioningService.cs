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
    private const float StartingCredits = 0;

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
            IsPublic = false,
            Role = UserRole.Basic,
            IsAdmin = false,
            Tokens = []
        };

        return await userRepository.Provision(user);
    }
}
