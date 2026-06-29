using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Interfaces;

/// <summary>
/// Represents a function that can be called in expressions (e.g., sma, ema, macd)
/// </summary>
public interface IFunction
{
    /// <summary>
    /// The name of the function as it appears in scripts
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the function with the given parameters
    /// </summary>
    /// <param name="parameters">Function parameters</param>
    /// <param name="context">Evaluation context</param>
    /// <returns>The result of the function execution</returns>
    object Execute(object[] parameters, ExpressionContext context);
}

/// <summary>
/// Represents a function that returns a numeric series (like indicators)
/// </summary>
public interface ISeriesFunction : IFunction
{
    // Inherits from IFunction but ensures it returns series data
}

/// <summary>
/// Optional interface for series functions that can update incrementally when new bars are appended.
/// Implementations should return an updated result of the same shape as Execute.
/// </summary>
public interface IIncrementalSeriesFunction : ISeriesFunction
{
    /// <summary>
    /// Append new results based on updated context, using a previous result as seed.
    /// </summary>
    /// <param name="parameters">Function parameters (already-evaluated argument objects)</param>
    /// <param name="context">Evaluation context with the up-to-date stock data</param>
    /// <param name="previousResult">Prior result returned by Execute or Append</param>
    /// <returns>Updated result containing the prior data and any newly-computed points</returns>
    object Append(object[] parameters, ExpressionContext context, object previousResult);
}

/// <summary>
/// Represents a function that returns a boolean result
/// </summary>
public interface IBooleanFunction : IFunction
{
    // Inherits from IFunction but ensures it returns boolean results
}
