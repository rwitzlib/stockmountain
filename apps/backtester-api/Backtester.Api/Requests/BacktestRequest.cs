using System.Diagnostics.CodeAnalysis;

namespace Backtester.Api.Requests;

[ExcludeFromCodeCoverage]
public class BacktestRequest
{
    public List<string> Filters { get; set; }
}
