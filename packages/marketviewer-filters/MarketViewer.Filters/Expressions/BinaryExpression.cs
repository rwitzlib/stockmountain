using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Expressions;

/// <summary>
/// Represents a binary expression with a left operand, operator, and right operand
/// </summary>
public class BinaryExpression : IExpression
{
    private readonly IExpression _left;
    private readonly IOperator _operator;
    private readonly IExpression _right;

    public BinaryExpression(IExpression left, IOperator op, IExpression right)
    {
        _left = left;
        _operator = op;
        _right = right;
    }

    public IExpression Left => _left;
    public IExpression Right => _right;
    public IOperator Operator => _operator;

    public object Evaluate(ExpressionContext context)
    {
        // Logical short-circuiting with cost-aware ordering
        if (_operator is ILogicalOperator)
        {
            // Analyze both sides for simple heuristics
            var leftInfo = ExpressionPlanner.Analyze(_left);
            var rightInfo = ExpressionPlanner.Analyze(_right);

            bool isAnd = string.Equals(_operator.Symbol, "AND", StringComparison.OrdinalIgnoreCase);
            bool isOr = string.Equals(_operator.Symbol, "OR", StringComparison.OrdinalIgnoreCase);

            IExpression first;
            IExpression second;

            if (isAnd)
            {
                // For AND, evaluate the child most likely to be false first; tie-breaker by lower cost
                // Lower selectivity means more likely false
                var pickLeftFirst = leftInfo.SelectivityHint <= rightInfo.SelectivityHint ||
                                    (Math.Abs(leftInfo.SelectivityHint - rightInfo.SelectivityHint) < 1e-6 && leftInfo.EstimatedCost <= rightInfo.EstimatedCost);

                first = pickLeftFirst ? _left : _right;
                second = pickLeftFirst ? _right : _left;

                var firstValue = Convert.ToBoolean(first.Evaluate(context));
                if (!firstValue) return false;

                var secondValue = Convert.ToBoolean(second.Evaluate(context));
                return firstValue && secondValue;
            }

            if (isOr)
            {
                // For OR, evaluate the child most likely to be true first; tie-breaker by lower cost
                var pickLeftFirst = leftInfo.SelectivityHint >= rightInfo.SelectivityHint ||
                                    (Math.Abs(leftInfo.SelectivityHint - rightInfo.SelectivityHint) < 1e-6 && leftInfo.EstimatedCost <= rightInfo.EstimatedCost);

                first = pickLeftFirst ? _left : _right;
                second = pickLeftFirst ? _right : _left;

                var firstValue = Convert.ToBoolean(first.Evaluate(context));
                if (firstValue) return true;

                var secondValue = Convert.ToBoolean(second.Evaluate(context));
                return firstValue || secondValue;
            }
        }

        // Default behavior for non-logical operators
        var leftEval = _left.Evaluate(context);
        var rightEval = _right.Evaluate(context);
        return _operator.Execute(leftEval, rightEval, context);
    }
}
