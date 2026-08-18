using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Market.Filters;

[ExcludeFromCodeCoverage]
public class FilterValidateRequest
{
    /// <summary>
    /// One or more filter expressions to validate. Batched so list views can resolve
    /// every filter in a single call.
    /// </summary>
    public required List<string> Expressions { get; init; }

    /// <summary>
    /// Optional evaluation context: "scan" | "backtest" | "chart". When set, functions and
    /// keywords not available in that context are reported as validation errors.
    /// </summary>
    public string? Context { get; init; }
}
