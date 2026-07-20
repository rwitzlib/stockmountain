using MarketViewer.Contracts.Models;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Responses.Tools;

[ExcludeFromCodeCoverage]
public class MassiveFidelityResponse
{
    public MassiveFidelity Minute { get; set; }
    public MassiveFidelity Hour { get; set; }
}
