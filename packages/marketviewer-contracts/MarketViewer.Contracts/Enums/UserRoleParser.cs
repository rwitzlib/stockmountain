namespace MarketViewer.Contracts.Enums;

public static class UserRoleParser
{
    /// <summary>
    /// Parses a stored role string, accepting the pre-billing legacy names still present on
    /// old user records ("Basic" → Free, "Advanced" → Pro). Writes always use the new names.
    /// </summary>
    public static UserRole Parse(string value) => value switch
    {
        "Basic" => UserRole.Free,
        "Advanced" => UserRole.Pro,
        _ => Enum.Parse<UserRole>(value)
    };
}
