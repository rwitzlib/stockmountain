using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics.CodeAnalysis;


namespace MarketViewer.Contracts.Responses.Tools;

[ExcludeFromCodeCoverage]
public class StatsResponse
{
    public MemoryCacheStatistics CacheStatistics { get; set; }
    public int TickerCount { get; set; }
    public int StocksResponseCount { get; set; }
}
