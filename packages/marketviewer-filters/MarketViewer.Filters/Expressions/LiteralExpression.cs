using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Expressions;

/// <summary>
/// Represents a literal value expression (constants, numbers, etc.)
/// </summary>
public class LiteralExpression : IExpression
{
    private readonly object _value;

    public LiteralExpression(object value, string? sourceText = null)
    {
        _value = value;
        SourceText = sourceText;
    }

    public object Evaluate(ExpressionContext context)
    {
        return _value;
    }

    /// <summary>
    /// Gets the literal value
    /// </summary>
    public object GetValue() => _value;

    /// <summary>
    /// The token as the user wrote it, when the value is a rewrite of it: a time literal such as
    /// <c>9:30</c> parses to 570 (minutes since midnight) but must print back as <c>9:30</c>.
    /// Null for plain numbers and identifiers, which print from <see cref="GetValue"/>.
    /// </summary>
    public string? SourceText { get; }
}
