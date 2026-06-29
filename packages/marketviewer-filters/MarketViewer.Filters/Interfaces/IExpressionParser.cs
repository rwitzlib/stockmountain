using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Interfaces;

/// <summary>
/// Parses script strings into executable expressions
/// </summary>
public interface IExpressionParser
{
    /// <summary>
    /// Parses a script string into an expression
    /// </summary>
    /// <param name="script">The script to parse</param>
    /// <returns>The parsed expression</returns>
    IExpression Parse(string script);
}

/// <summary>
/// Represents a parsed expression tree node
/// </summary>
public interface IExpressionNode
{
    /// <summary>
    /// Evaluates this node
    /// </summary>
    /// <param name="context">Evaluation context</param>
    /// <returns>The result of evaluation</returns>
    object Evaluate(ExpressionContext context);
}
