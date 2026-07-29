using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Market.Scan;

[ExcludeFromCodeCoverage]
public class ScanRequest : BaseRequest
{
    public DateTimeOffset? Timestamp { get; set; }
    public List<string> Filters { get; set; }

    /// <summary>
    /// When true, filters evaluate completed minute bars only — the in-progress
    /// (partial) live bar is excluded. Matches backtest semantics: the backtester
    /// only ever sees completed canonical bars, and the partial bar structurally
    /// undercounts volume (dark-pool/TRF prints are reported late; see ADR 0003).
    /// </summary>
    public bool CompletedBarsOnly { get; set; }
}
