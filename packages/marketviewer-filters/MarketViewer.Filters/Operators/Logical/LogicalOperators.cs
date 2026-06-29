using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Operators.Logical;

/// <summary>
/// Logical AND operator
/// </summary>
public class AndOperator : ILogicalOperator
{
    public string Symbol => "AND";
    public int Precedence => 2;
    public bool IsBinary => true;
    public bool IsUnary => false;

    public object Execute(object? left, object right, ExpressionContext context)
    {
        ArgumentNullException.ThrowIfNull(left);

        var leftBool = Convert.ToBoolean(left);
        var rightBool = Convert.ToBoolean(right);

        return leftBool && rightBool;
    }
}
