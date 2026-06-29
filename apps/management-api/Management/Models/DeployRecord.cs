using Management.Enums;
using System.Diagnostics.CodeAnalysis;

namespace Management.Models;

[ExcludeFromCodeCoverage]
public class DeployRecord
{
    public string Id { get; set; }
    public string Environment { get; set; }
    public string Repository { get; set; }
    public string File { get; set; }
    public string Image { get; set; }
    public DeployStatus Status { get; set; }
    public DeployType Type { get; set; }
    public string CreatedAt { get; set; }
    public string CreatedBy { get; set; }
}
