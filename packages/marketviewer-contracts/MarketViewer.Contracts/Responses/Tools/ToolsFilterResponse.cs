using Polygon.Client.Models;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Responses.Tools;

[ExcludeFromCodeCoverage]
public class ToolsFilterResponse
{
    public List<Bar> Results { get; set; }
    public List<long> MatchingTimestamps { get; set; }
}
