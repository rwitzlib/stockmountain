using MarketViewer.Filters.Interfaces;
using MarketViewer.Filters.Parsing;

namespace MarketViewer.Filters.Expressions;

/// <summary>
/// Structural facts about a parsed line that the bracket-suffix rules and the canonical printer
/// depend on (plan 20, decisions 2 and 3).
/// </summary>
public static class ExpressionShape
{
    /// <summary>
    /// True when nothing on the line is computed from bars: only per-ticker scalars (<c>float</c>),
    /// numbers and identifiers. Such a line evaluates once per ticker, so a
    /// <c>[timeframe, candles, mode]</c> suffix has nothing to apply to and is rejected.
    /// </summary>
    public static bool IsScalarOnly(IExpression expression) => !ProducesSeries(expression);

    /// <summary>
    /// True when every condition on the line is a boolean function call (<c>crosses_over</c>,
    /// <c>crosses_under</c>) and no comparison operator appears. Crosses are inherently
    /// any-of-range, so an explicit <c>all</c> is rejected and the printer emits <c>any</c>.
    /// </summary>
    public static bool IsCrossOnly(IExpression expression) => HasBooleanFunction(expression) && !HasComparison(expression);

    /// <summary>
    /// True for the evaluation clock (<c>time</c>, <c>time.hour</c>, <c>time.minute</c>): a single value
    /// per evaluation, not a per-candle series, so a candle window never applies to it.
    /// </summary>
    public static bool IsClock(IExpression expression) => expression switch
    {
        DataAccessExpression data => data.GetFieldName() == "time",
        FieldAccessExpression field => IsClock(field.GetTargetExpression()),
        _ => false,
    };

    /// <summary>True when at least one comparison operator appears on the line.</summary>
    public static bool HasComparison(IExpression expression) => expression switch
    {
        TimeframeRangeExpression range => HasComparison(range.GetInnerExpression()),
        UnaryExpression unary => HasComparison(unary.Operand),
        BinaryExpression binary => binary.Operator is IComparisonOperator
            || HasComparison(binary.Left) || HasComparison(binary.Right),
        _ => false,
    };

    /// <summary>True when at least one boolean function call (a cross) appears on the line.</summary>
    public static bool HasBooleanFunction(IExpression expression) => expression switch
    {
        TimeframeRangeExpression range => HasBooleanFunction(range.GetInnerExpression()),
        UnaryExpression unary => HasBooleanFunction(unary.Operand),
        BinaryExpression binary => HasBooleanFunction(binary.Left) || HasBooleanFunction(binary.Right),
        FunctionCallExpression call => call.GetFunction() is IBooleanFunction,
        _ => false,
    };

    private static bool ProducesSeries(IExpression expression) => expression switch
    {
        TimeframeRangeExpression range => ProducesSeries(range.GetInnerExpression()),
        UnaryExpression unary => ProducesSeries(unary.Operand),
        BinaryExpression binary => ProducesSeries(binary.Left) || ProducesSeries(binary.Right),
        FieldAccessExpression field => ProducesSeries(field.GetTargetExpression()),
        // Every function runs over bars (indicators, transforms, crosses), even with literal arguments.
        FunctionCallExpression => true,
        DataAccessExpression data => !data.IsScalar,
        _ => false,
    };
}
