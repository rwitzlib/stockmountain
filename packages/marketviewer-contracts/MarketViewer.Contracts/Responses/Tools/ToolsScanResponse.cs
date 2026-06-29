using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Responses.Market;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Responses.Tools;

[ExcludeFromCodeCoverage]
public class ToolsScanResponse
{
    public string Ticker { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public ToolsScanResult Live { get; set; }
    public ToolsScanResult Backtest { get; set; }
}

[ExcludeFromCodeCoverage]
public class ToolsScanResult
{
    public bool IsSuccess { get; set; }
    //public List<Filter> PassedFilters { get; set; }
    //public List<Filter> FailedFilters { get; set; }
    public Dictionary<Timespan, StocksResponse> StocksResponses { get; set; }
}
