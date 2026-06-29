using MarketViewer.Filters.Interfaces;
using MarketViewer.Contracts.Models;
using MarketViewer.Filters;

namespace MarketViewer.Filters.Expressions;

/// <summary>
/// Expression wrapper that evaluates with specific timeframe and range context
/// </summary>
public class TimeframeRangeExpression : IExpression
{
    private readonly IExpression _innerExpression;
    private readonly Timeframe? _timeframe;
    private readonly int? _range;
    private readonly RangeEvaluationMode? _evaluationMode;

    public TimeframeRangeExpression(IExpression innerExpression, Timeframe? timeframe, int? range, RangeEvaluationMode? evaluationMode)
    {
        _innerExpression = innerExpression;
        _timeframe = timeframe;
        _range = range;
        _evaluationMode = evaluationMode;
    }

    /// <summary>
    /// Gets the timeframe specified in this expression
    /// </summary>
    /// <returns>The timeframe if specified, null otherwise</returns>
    public Timeframe? GetTimeframe()
    {
        return _timeframe;
    }

    /// <summary>
    /// Gets the range specified in this expression
    /// </summary>
    /// <returns>The range if specified, null otherwise</returns>
    public int? GetRange()
    {
        return _range;
    }

    public RangeEvaluationMode? GetRangeEvaluationMode()
    {
        return _evaluationMode;
    }

    public IExpression GetInnerExpression()
    {
        return _innerExpression;
    }

    public object Evaluate(ExpressionContext context)
    {
        // Create a modified context with the specified timeframe and range
        var modifiedContext = new ExpressionContext
        {
            StockData = context.StockData,
            Timeframe = _timeframe ?? context.Timeframe,
            Parameters = context.Parameters,
            CandleRange = _range ?? context.CandleRange,
            RangeEvaluationMode = _evaluationMode ?? context.RangeEvaluationMode
        };

        return _innerExpression.Evaluate(modifiedContext);
    }
}
