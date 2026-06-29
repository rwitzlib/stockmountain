using Management.Enums;
using System.Diagnostics.CodeAnalysis;

namespace Management.Response;

[ExcludeFromCodeCoverage]
public class DeployResponse
{
    public string Id { get; set; }
    public DeployStatus Status { get; set; }
    public List<string> ErrorMessages { get; set; }
}
