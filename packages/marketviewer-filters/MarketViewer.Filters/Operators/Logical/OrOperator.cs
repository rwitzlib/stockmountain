using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Operators.Logical;

/// <summary>
/// Logical OR operator
/// </summary>
public class OrOperator : ILogicalOperator
{
    public string Symbol => "OR";
    public int Precedence => 1;
    public bool IsBinary => true;
    public bool IsUnary => false;

    public object Execute(object? left, object right, ExpressionContext context)
    {
        ArgumentNullException.ThrowIfNull(left);

        var leftBool = Convert.ToBoolean(left);
        var rightBool = Convert.ToBoolean(right);

        return leftBool || rightBool;
    }
}
