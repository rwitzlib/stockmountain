using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Enums;

/// <summary>
/// Purchase tier for a user. Ordered so that higher tiers satisfy lower-tier requirements.
/// Admin access is a separate flag on UserRecord, not a tier, so subscription changes can
/// never grant or revoke admin rights.
/// Stored user records may still carry the pre-billing names ("Basic", "Advanced") —
/// read them through <see cref="UserRoleParser"/>, never Enum.Parse.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<UserRole>))]
public enum UserRole
{
    Free = 1,
    Pro = 2,
    Premium = 3
}
