using MarketViewer.Contracts.Models;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Responses.Market.Filters;

[ExcludeFromCodeCoverage]
public class FilterValidateResponse
{
    public required List<FilterValidationResult> Results { get; init; }
}

[ExcludeFromCodeCoverage]
public class FilterValidationResult
{
    public required string Expression { get; init; }
    public bool Valid { get; init; }
    public string? Error { get; init; }

    /// <summary>Human-readable phrasing of the expression, e.g. "RSI(14) on the 1m chart is below 30".</summary>
    public string? Description { get; init; }

    public Timeframe? Timeframe { get; init; }
    public FilterAstNode? Ast { get; init; }
}

/// <summary>
/// Presentation-oriented projection of the parser's expression tree — just enough
/// structure for chip rendering and segment editing, not for evaluation.
/// </summary>
[ExcludeFromCodeCoverage]
public class FilterAstNode
{
    /// <summary>binary | unary | function | field | data | literal | range</summary>
    public required string Kind { get; init; }

    public string? Op { get; init; }                  // binary / unary (NOT)
    public FilterAstNode? Left { get; init; }         // binary
    public FilterAstNode? Right { get; init; }        // binary

    public string? Name { get; init; }                // function
    public List<FilterAstNode>? Args { get; init; }   // function

    public string? Field { get; init; }               // field / data
    public FilterAstNode? Target { get; init; }       // field

    public string? Value { get; init; }               // literal

    public FilterAstNode? Inner { get; init; }        // range / unary
    public Timeframe? Timeframe { get; init; }        // range
    public int? Candles { get; init; }                // range
}
