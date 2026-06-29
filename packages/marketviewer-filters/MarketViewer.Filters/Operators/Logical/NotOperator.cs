using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Operators.Logical;

/// <summary>
/// Logical NOT operator
/// </summary>
public class NotOperator : ILogicalOperator
{
    public string Symbol => "NOT";
    public int Precedence => 3;
    public bool IsBinary => false;
    public bool IsUnary => true;

    public object Execute(object? left, object right, ExpressionContext context)
    {
        // For unary NOT, left is null and right contains the operand
        var operandBool = Convert.ToBoolean(right);
        return !operandBool;
    }
}
