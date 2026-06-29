using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Expressions;

/// <summary>
/// Represents a literal value expression (constants, numbers, etc.)
/// </summary>
public class LiteralExpression : IExpression
{
    private readonly object _value;

    public LiteralExpression(object value)
    {
        _value = value;
    }

    public object Evaluate(ExpressionContext context)
    {
        return _value;
    }

    /// <summary>
    /// Gets the literal value
    /// </summary>
    public object GetValue() => _value;
}
