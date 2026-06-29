using System.Diagnostics.CodeAnalysis;

namespace Management.Requests;

[ExcludeFromCodeCoverage]
public class DeployRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Environment { get; set; }
    public string Repository { get; set; }
    public string File { get; set; }
    public string Image { get; set; }
    public string Actor { get; set; }
}
