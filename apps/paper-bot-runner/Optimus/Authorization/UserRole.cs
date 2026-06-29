using System.Text.Json.Serialization;

namespace Optimus.Authorization;

[JsonConverter(typeof(JsonStringEnumConverter<UserRole>))]
public enum UserRole
{
    None,
    Starter,
    Advanced,
    Premium,
    Admin
}
