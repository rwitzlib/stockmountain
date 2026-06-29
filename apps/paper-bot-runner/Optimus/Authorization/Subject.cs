using System.Diagnostics.CodeAnalysis;

namespace Optimus.Authorization;

[ExcludeFromCodeCoverage]
public class Subject
{
    public string Email { get; set; }
    public UserRole Role { get; set; }
}