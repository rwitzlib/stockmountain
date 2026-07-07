using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests;

[ExcludeFromCodeCoverage]
public class BaseRequest
{
    public string Id { get; } = Guid.NewGuid().ToString();
}
