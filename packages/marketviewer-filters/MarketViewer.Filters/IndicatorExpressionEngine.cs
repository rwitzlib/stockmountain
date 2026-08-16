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
    /// Evaluates a series-producing script (e.g. "sma(20)", "macd(12,26,9,ema).histogram",
    /// "slope(close,5)") and returns one value per bar in <paramref name="stockData"/>, aligned by
    /// index; <c>null</c> where the indicator has no value yet (warm-up).
    /// Used by golden tests and entry-snapshot capture; not part of the boolean filter path.
    /// </summary>
    /// <exception cref="InvalidOperationException">The script does not evaluate to a series or scalar.</exception>
    public double?[] EvaluateSeries(string script, StocksResponse stockData, Timeframe timeframe, DateTimeOffset? evaluationTime = null)
    {
        var expression = ParseExpression(script);
        if (expression is Expressions.TimeframeRangeExpression tf)
        {
            timeframe = tf.GetTimeframe() ?? timeframe;
            expression = tf.GetInnerExpression();
        }

        var context = new ExpressionContext
        {
            StockData = stockData,
            Timeframe = timeframe,
            EvaluationTime = evaluationTime
        };

        var bars = stockData.Results;
        var output = new double?[bars.Count];
        var result = expression.Evaluate(context);

        switch (result)
        {
            case List<IIndicatorResult> series:
            {
                // Align by timestamp so a function that skips bars still lands on the right index.
                var indexByTimestamp = new Dictionary<long, int>(bars.Count);
                for (int i = 0; i < bars.Count; i++)
                {
                    indexByTimestamp[bars[i].Timestamp] = i;
                }

                foreach (var point in series)
                {
                    if (!indexByTimestamp.TryGetValue(point.Timestamp, out var index))
                    {
                        throw new InvalidOperationException($"Series point at {point.Timestamp} does not match any bar in the input.");
                    }
                    output[index] = point.GetFieldValue("value");
                }
                break;
            }
            case List<double> values:
            {
                // Field-access and transform results carry no timestamps; they are right-aligned to the input.
                if (values.Count > bars.Count)
                {
                    throw new InvalidOperationException($"Series has {values.Count} points for {bars.Count} bars.");
                }
                var offset = bars.Count - values.Count;
                for (int i = 0; i < values.Count; i++)
                {
                    output[offset + i] = values[i];
                }
                break;
            }
            case double scalar:
                if (bars.Count > 0) output[^1] = scalar;
                break;
            case IIndicatorResult single:
                if (bars.Count > 0) output[^1] = single.GetFieldValue("value");
                break;
            default:
                throw new InvalidOperationException($"Script '{script}' evaluated to {result?.GetType().Name ?? "null"}, not a series.");
        }

        return output;
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
