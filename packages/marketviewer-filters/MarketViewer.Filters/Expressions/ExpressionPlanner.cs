using MarketViewer.Filters.Interfaces;
using MarketViewer.Filters.Parsing;

namespace MarketViewer.Filters.Expressions;

public static class ExpressionPlanner
{
    public static ExpressionHeuristics Analyze(IExpression expression)
    {
        // Very lightweight analysis: inspect known expression types
        switch (expression)
        {
            case FunctionCallExpression funcCall:
                var (cost, selectivity) = FunctionHeuristicsRegistry.GetHeuristics(funcCall.FunctionName);
                return new ExpressionHeuristics(cost, selectivity);
            case DataAccessExpression _:
                return new ExpressionHeuristics(1, 0.5);
            case FieldAccessExpression field:
                // Cost is cost of evaluating the target expression; field extraction is cheap
                var target = Analyze(field.GetTargetExpression());
                return new ExpressionHeuristics(target.EstimatedCost + 0.05, target.SelectivityHint);
            case UnaryExpression _:
                return new ExpressionHeuristics(1, 0.5);
            case BinaryExpression binary:
                // Estimate cost by analyzing children and operator type
                var left = Analyze(binary.Left);
                var right = Analyze(binary.Right);

                var isAnd = string.Equals(binary.Operator.Symbol, "AND", StringComparison.OrdinalIgnoreCase);
                var isOr = string.Equals(binary.Operator.Symbol, "OR", StringComparison.OrdinalIgnoreCase);

                // Logical nodes: cost ~ min(child costs) due to short-circuit, plus small overhead
                if (isAnd || isOr)
                {
                    // Compute expected cost for both possible orders and take the min
                    double shortCircuitLeft = isAnd ? (1.0 - left.SelectivityHint) : left.SelectivityHint;
                    double shortCircuitRight = isAnd ? (1.0 - right.SelectivityHint) : right.SelectivityHint;

                    double expectedLeftFirst = left.EstimatedCost + (1.0 - shortCircuitLeft) * right.EstimatedCost;
                    double expectedRightFirst = right.EstimatedCost + (1.0 - shortCircuitRight) * left.EstimatedCost;
                    double expectedCost = Math.Min(expectedLeftFirst, expectedRightFirst) + 0.1; // overhead

                    double combinedSelectivity = isAnd
                        ? left.SelectivityHint * right.SelectivityHint
                        : 1.0 - (1.0 - left.SelectivityHint) * (1.0 - right.SelectivityHint);

                    return new ExpressionHeuristics(expectedCost, combinedSelectivity);
                }

                // Comparison nodes: must evaluate both sides
                return new ExpressionHeuristics(left.EstimatedCost + right.EstimatedCost + 0.1, 0.5);
            case TimeframeRangeExpression _:
                // Timeframe wrapper adds negligible cost itself; analyze inner
                var inner = Analyze(((TimeframeRangeExpression)expression).GetInnerExpression());
                return new ExpressionHeuristics(inner.EstimatedCost, inner.SelectivityHint);
            case LiteralExpression _:
                // Literals are essentially free
                return new ExpressionHeuristics(0.01, 0.5);
            default:
                return new ExpressionHeuristics(1, 0.5);
        }
    }

    // Helper removed; ordering is computed symmetrically in expected cost calculation
}

public readonly record struct ExpressionHeuristics(double EstimatedCost, double SelectivityHint);

