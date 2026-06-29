using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Contracts.Models;
using MarketViewer.Filters;

namespace MarketViewer.Filters.Interfaces;

/// <summary>
/// Represents an expression that can be evaluated to a result
/// </summary>
public interface IExpression
{
    /// <summary>
    /// Evaluates the expression for the given market data context
    /// </summary>
    /// <param name="context">The evaluation context containing market data and timeframe</param>
    /// <returns>The result of evaluating the expression</returns>
    object Evaluate(ExpressionContext context);
}

/// <summary>
/// Context for expression evaluation containing market data and parameters
/// </summary>
public class ExpressionContext
{
    public required StocksResponse StockData { get; init; }
    public required Timeframe Timeframe { get; init; }
    public Dictionary<string, object>? Parameters { get; init; }

    /// <summary>
    /// Optional candle range for historical evaluation (e.g., check last N candles)
    /// </summary>
    public int? CandleRange { get; init; }

    /// <summary>
    /// Determines how range-based evaluations should aggregate candle results.
    /// </summary>
    public RangeEvaluationMode RangeEvaluationMode { get; init; } = RangeEvaluationMode.All;
}
