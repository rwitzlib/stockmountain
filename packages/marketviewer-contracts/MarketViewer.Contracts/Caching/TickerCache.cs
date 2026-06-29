
namespace MarketViewer.Contracts.Caching;

public class TickerCache
{
    public string Version { get; set; }
    public string[] IdToSymbol { get; set; }
    public Dictionary<string, int> SymbolToId { get; set; } = new Dictionary<string, int>();
}
