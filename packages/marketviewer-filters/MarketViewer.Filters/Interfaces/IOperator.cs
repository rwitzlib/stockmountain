using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Interfaces;

/// <summary>
/// Represents an operator that can combine expressions or values
/// </summary>
public interface IOperator
{
    /// <summary>
    /// The symbol or name of the operator
    /// </summary>
    string Symbol { get; }

    /// <summary>
    /// The precedence of the operator (higher numbers = higher precedence)
    /// </summary>
    int Precedence { get; }

    /// <summary>
    /// Whether this is a binary operator (takes two operands)
    /// </summary>
    bool IsBinary { get; }

    /// <summary>
    /// Whether this is a unary operator (takes one operand)
    /// </summary>
    bool IsUnary { get; }

    /// <summary>
    /// Executes the operator with the given operands
    /// </summary>
    /// <param name="left">Left operand (null for unary operators)</param>
    /// <param name="right">Right operand</param>
    /// <param name="context">Evaluation context</param>
    /// <returns>The result of the operation</returns>
    object Execute(object? left, object right, ExpressionContext context);
}

/// <summary>
/// Represents a logical operator (AND, OR, NOT)
/// </summary>
public interface ILogicalOperator : IOperator
{
    // Inherits from IOperator but ensures boolean operations
}

/// <summary>
/// Represents a comparison operator (>, <, =, etc.)
/// </summary>
public interface IComparisonOperator : IOperator
{
    // Inherits from IOperator but ensures comparison operations
}
