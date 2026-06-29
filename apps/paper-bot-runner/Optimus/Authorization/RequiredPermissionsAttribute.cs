using Microsoft.AspNetCore.Authorization;

namespace Optimus.Authorization;

public class RequiredPermissionsAttribute : AuthorizeAttribute
{
    public UserRole[] Permission { get; }

    public RequiredPermissionsAttribute(params UserRole[] permission)
    {
        Permission = permission;
        
        // Create a unique policy name that includes the roles
        var roleNames = string.Join(",", permission.Select(r => r.ToString()));
        Policy = $"RequiredPermissions:{roleNames}";
    }
}
