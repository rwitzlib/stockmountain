using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Enums;

/// <summary>
/// Purchase tier for a user. Ordered so that higher tiers satisfy lower-tier requirements.
/// Admin access is a separate flag on UserRecord, not a tier, so subscription changes can
/// never grant or revoke admin rights.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<UserRole>))]
public enum UserRole
{
    Basic = 1,
    Advanced = 2,
    Premium = 3
}
