using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market.Backtest;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Market.Backtest;

[ExcludeFromCodeCoverage]
public class GetBacktestResultRequest : BaseRequest
{
    public string Id { get; set; }
}
