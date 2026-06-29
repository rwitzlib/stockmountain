using System.Diagnostics.CodeAnalysis;
using MarketViewer.Contracts.Records;

namespace MarketViewer.Contracts.Responses.Management;

[ExcludeFromCodeCoverage]
public class TradeResponse
{
    // Basic metrics
    public int TotalTrades { get; set; }
    public float TotalProfit { get; set; }
    public float AverageProfit { get; set; }
    public float WinRate { get; set; }

    // Concurrent exposure proxy
    public int MaxConcurrentTrades { get; set; }

    public IEnumerable<TradeRecord> Trades { get; set; } = [];
}



