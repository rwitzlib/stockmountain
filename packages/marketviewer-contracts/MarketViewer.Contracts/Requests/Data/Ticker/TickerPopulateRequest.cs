using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Data.Ticker;

[ExcludeFromCodeCoverage]
public class TickerPopulateRequest
{
    public List<string> Markets { get; set; } = [];
    public bool Active { get; set; } = true;
    public List<string> Types { get; set; } = [];
}
