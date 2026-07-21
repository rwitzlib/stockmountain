using MarketViewer.Filters.Interfaces;
using MarketViewer.Filters.Parsing;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Contracts.Models;

namespace MarketViewer.Filters;

/// <summary>
/// Main engine for parsing and evaluating indicator expressions
/// </summary>
public class IndicatorExpressionEngine
{
    private readonly IExpressionParser _parser;

    public IndicatorExpressionEngine()
    {
        _parser = new ExpressionParser();
    }

    /// <summary>
    /// Parses a script expression into an executable expression
    /// </summary>
    /// <param name="script">The script to parse (e.g., "sma(20) > sma(50)")</param>
    /// <returns>An executable expression</returns>
    public IExpression ParseExpression(string script)
    {
        return _parser.Parse(script);
    }

    /// <summary>
    /// Evaluates an expression against the given market data
    /// </summary>
    /// <param name="expression">The expression to evaluate</param>
    /// <param name="stockData">The stock data to evaluate against</param>
    /// <param name="timeframe">The timeframe for the evaluation</param>
    /// <param name="parameters">Optional parameters</param>
    /// <param name="evaluationTime">The clock for this evaluation (scan time or simulated backtest time); drives the "time" field</param>
    /// <returns>True if the expression evaluates to true</returns>
    public bool EvaluateExpression(IExpression expression, StocksResponse stockData, Timeframe timeframe, Dictionary<string, object>? parameters = null, DateTimeOffset? evaluationTime = null)
    {
        var context = new ExpressionContext
        {
            StockData = stockData,
            Timeframe = timeframe,
            Parameters = parameters,
            EvaluationTime = evaluationTime
        };

        var result = expression.Evaluate(context);

        // Convert the result to boolean
        return result switch
        {
            bool boolResult => boolResult,
            double doubleResult => doubleResult > 0,
            float floatResult => floatResult > 0,
            int intResult => intResult > 0,
            _ => false
        };
    }

    /// <summary>
    /// Convenience method to parse and evaluate a script in one call
    /// </summary>
    /// <param name="script">The script to parse and evaluate</param>
    /// <param name="stockData">The stock data to evaluate against</param>
    /// <param name="timeframe">The timeframe for the evaluation</param>
    /// <param name="parameters">Optional parameters</param>
    /// <returns>True if the expression evaluates to true</returns>
    public bool EvaluateScript(string script, StocksResponse stockData, Timeframe timeframe, Dictionary<string, object>? parameters = null, DateTimeOffset? evaluationTime = null)
    {
        var expression = ParseExpression(script);
        return EvaluateExpression(expression, stockData, timeframe, parameters, evaluationTime);
    }

    /// <summary>
    /// Compiles a script into a reusable session that supports incremental evaluation.
    /// </summary>
    public Sessions.FilterSession Compile(string script)
    {
        var expression = ParseExpression(script);
        return new Sessions.FilterSession(expression);
    }

    /// <summary>
    /// Extracts the timeframe from a parsed expression for data retrieval planning
    /// </summary>
    /// <param name="expression">The parsed expression to analyze</param>
    /// <returns>The timeframe if specified in the expression, null otherwise</returns>
    public Timeframe? ExtractTimeframe(IExpression expression)
    {
        // Check if the expression is a TimeframeRangeExpression
        if (expression is MarketViewer.Filters.Expressions.TimeframeRangeExpression timeframeExpression)
        {
            return timeframeExpression.GetTimeframe();
        }

        return null;
    }

    /// <summary>
    /// Parses a script and extracts the timeframe for data retrieval planning
    /// </summary>
    /// <param name="script">The script to parse (e.g., "sma(20) > sma(50) [5m, 3]")</param>
    /// <returns>The timeframe if specified in the script, null otherwise</returns>
    public Timeframe? ExtractTimeframeFromScript(string script)
    {
        var expression = ParseExpression(script);
        return ExtractTimeframe(expression);
    }
}
