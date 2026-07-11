using System;
using System.Globalization;
using MarketViewer.Filters.Expressions;
using MarketViewer.Filters.Interfaces;
using MarketViewer.Filters.Operators.Comparison;
using MarketViewer.Filters.Operators.Logical;
using System.Text.RegularExpressions;
using MarketViewer.Filters.Functions.Indicators;
using MarketViewer.Filters.Functions.Comparison;
using MarketViewer.Contracts.Enums;
using MarketViewer.Filters.Functions.Transforms;
using MarketViewer.Contracts.Models;
using MarketViewer.Filters;

namespace MarketViewer.Filters.Parsing;

/// <summary>
/// Basic expression parser for indicator scripts
/// </summary>
public class ExpressionParser : IExpressionParser
{
    private readonly Dictionary<string, IFunction> _functions;
    private readonly Dictionary<string, IOperator> _operators;

    public ExpressionParser()
    {
        // Register built-in functions
        var supportResistance = new SupportResistanceFunction();
        _functions = new Dictionary<string, IFunction>(StringComparer.OrdinalIgnoreCase)
        {
            ["sma"] = new SmaFunction(),
            ["ema"] = new EmaFunction(),
            ["macd"] = new MacdFunction(),
            ["adv"] = new AdvFunction(),
            ["crosses_over"] = new CrossesOverFunction(),
            ["crosses_under"] = new CrossesUnderFunction(),
            ["rsi"] = new RsiFunction(),
            ["slope"] = new SlopeFunction(),
            ["support_resistance"] = supportResistance,
            ["sr"] = supportResistance,
        };

        // Register built-in operators
        _operators = new Dictionary<string, IOperator>(StringComparer.OrdinalIgnoreCase)
        {
            [">"] = new GreaterThanOperator(),
            [">="] = new GreaterThanOrEqualOperator(),
            ["<"] = new LessThanOperator(),
            ["<="] = new LessThanOrEqualOperator(),
            ["="] = new EqualOperator(),
            ["!="] = new NotEqualOperator(),
            ["AND"] = new AndOperator(),
            ["OR"] = new OrOperator(),
            ["NOT"] = new NotOperator()
        };
    }

    public IExpression Parse(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
            throw new ArgumentException("Script cannot be null or empty");

        // Check for timeframe/range syntax like "expression [5m, 3]"
        var (expressionScript, timeframe, range, evaluationMode) = ParseTimeframeAndRange(script.Trim());

        // Tokenize the expression part
        var tokens = Tokenize(expressionScript);

        // Parse the expression
        var expression = ParseExpression(tokens, 0).expression;

        // If timeframe/range specified, wrap in a context-aware expression
        if (timeframe != null || range.HasValue || evaluationMode.HasValue)
        {
            return new TimeframeRangeExpression(expression, timeframe, range, evaluationMode);
        }

        return expression;
    }

    private (string expression, Timeframe? timeframe, int? range, RangeEvaluationMode? mode) ParseTimeframeAndRange(string script)
    {
        // Look for [timeframe, range] or [timeframe] at the end
        var bracketStart = script.LastIndexOf('[');
        var bracketEnd = script.LastIndexOf(']');

        if (bracketStart == -1 || bracketEnd == -1 || bracketEnd < bracketStart)
        {
            return (script, null, null, null);
        }

        var expressionPart = script.Substring(0, bracketStart).Trim();
        var timeframeRangePart = script.Substring(bracketStart + 1, bracketEnd - bracketStart - 1).Trim();

        // Parse timeframe and range from the bracket part
        var parts = timeframeRangePart.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        Timeframe? timeframe = null;
        int? range = null;
        RangeEvaluationMode? evaluationMode = null;

        foreach (var rawPart in parts)
        {
            var part = rawPart;

            // Range: pure integer (e.g., 3)
            if (int.TryParse(part, out var rangeValue))
            {
                range = rangeValue;
                continue;
            }

            if (TryParseRangeEvaluationMode(part, out var parsedMode))
            {
                evaluationMode = parsedMode;
                continue;
            }

            // Timeframe with quantity (e.g., 5m, 15m, 1h, 2d)
            // Pattern: one or more digits followed by letters
            var match = Regex.Match(part, @"^(?<qty>\d+)\s*(?<unit>[a-zA-Z]+)$");
            if (match.Success)
            {
                var qty = int.Parse(match.Groups["qty"].Value);
                var unitToken = match.Groups["unit"].Value;

                if (Enum.TryParse<Timespan>(NormalizeTimespanUnit(unitToken), true, out var timespanWithQty))
                {
                    timeframe = new Timeframe(qty, timespanWithQty);
                    continue;
                }
            }

            // Timeframe without quantity (assume 1 unit)
            if (Enum.TryParse<Timespan>(NormalizeTimespanUnit(part), true, out var timespan))
            {
                timeframe = new Timeframe(1, timespan);
            }
        }

        return (expressionPart, timeframe, range, evaluationMode);
    }

    private static bool TryParseRangeEvaluationMode(string value, out RangeEvaluationMode mode)
    {
        if (value.Equals("any", StringComparison.OrdinalIgnoreCase))
        {
            mode = RangeEvaluationMode.Any;
            return true;
        }

        if (value.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            mode = RangeEvaluationMode.All;
            return true;
        }

        mode = default;
        return false;
    }

    private static string NormalizeTimespanUnit(string unit)
    {
        // Accept common shorthand and map to enum names as needed
        // Assuming enum values like: minute, hour, day, week, month, etc.
        var token = unit.Trim().ToLowerInvariant();
        return token switch
        {
            "m" or "min" or "mins" or "minute" or "minutes" => nameof(Timespan.minute),
            "h" or "hr" or "hrs" or "hour" or "hours" => nameof(Timespan.hour),
            "d" or "day" or "days" => nameof(Timespan.day),
            "w" or "wk" or "wks" or "week" or "weeks" => nameof(Timespan.week),
            "mo" or "mon" or "month" or "months" => nameof(Timespan.month),
            _ => unit
        };
    }

    private static List<string> Tokenize(string script)
    {
        // Very basic tokenizer - splits on whitespace and operators
        // This is a simplified version; a real parser would be more sophisticated
        var tokens = new List<string>();
        var currentToken = "";

        for (int i = 0; i < script.Length; i++)
        {
            var c = script[i];

            if (char.IsWhiteSpace(c))
            {
                if (!string.IsNullOrEmpty(currentToken))
                {
                    tokens.Add(currentToken);
                    currentToken = "";
                }
            }
            else if (c == '(' || c == ')' || c == ',' || c == '.')
            {
                // Allow decimal points within numeric tokens by checking neighbors.
                if (c == '.' && IsPotentialDecimalSeparator(script, i, currentToken))
                {
                    currentToken += c;
                    continue;
                }

                if (!string.IsNullOrEmpty(currentToken))
                {
                    tokens.Add(currentToken);
                    currentToken = "";
                }
                tokens.Add(c.ToString());
            }
            else if (c == '!')
            {
                if (!string.IsNullOrEmpty(currentToken))
                {
                    tokens.Add(currentToken);
                    currentToken = "";
                }
                // Check for !=
                if (i + 1 < script.Length && script[i + 1] == '=')
                {
                    tokens.Add("!=");
                    i++;
                }
                else
                {
                    tokens.Add("!");
                }
            }
            else if (c == '>')
            {
                if (!string.IsNullOrEmpty(currentToken))
                {
                    tokens.Add(currentToken);
                    currentToken = "";
                }
                // Check for >=
                if (i + 1 < script.Length && script[i + 1] == '=')
                {
                    tokens.Add(">=");
                    i++;
                }
                else
                {
                    tokens.Add(">");
                }
            }
            else if (c == '<')
            {
                if (!string.IsNullOrEmpty(currentToken))
                {
                    tokens.Add(currentToken);
                    currentToken = "";
                }
                // Check for <=
                if (i + 1 < script.Length && script[i + 1] == '=')
                {
                    tokens.Add("<=");
                    i++;
                }
                else
                {
                    tokens.Add("<");
                }
            }
            else if (c == '=')
            {
                if (!string.IsNullOrEmpty(currentToken))
                {
                    tokens.Add(currentToken);
                    currentToken = "";
                }
                // Check for ==
                if (i + 1 < script.Length && script[i + 1] == '=')
                {
                    tokens.Add("==");
                    i++;
                }
                else
                {
                    tokens.Add("=");
                }
            }
            else
            {
                currentToken += c;
            }
        }

        if (!string.IsNullOrEmpty(currentToken))
        {
            tokens.Add(currentToken);
        }

        return tokens;
    }

    private (IExpression expression, int nextIndex) ParseExpression(List<string> tokens, int index)
    {
        // Parse logical expressions (AND, OR)
        var (left, nextIndex) = ParseComparison(tokens, index);

        while (nextIndex < tokens.Count)
        {
            var token = tokens[nextIndex];
            if (token.Equals("AND", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("OR", StringComparison.OrdinalIgnoreCase))
            {
                if (!_operators.TryGetValue(token.ToUpper(), out var op))
                    throw new InvalidOperationException($"Unknown operator: {token}");

                var (right, newIndex) = ParseComparison(tokens, nextIndex + 1);
                left = new BinaryExpression(left, op, right);
                nextIndex = newIndex;
            }
            else
            {
                break;
            }
        }

        return (left, nextIndex);
    }

    private (IExpression expression, int nextIndex) ParseComparison(List<string> tokens, int index)
    {
        // Parse comparison expressions (>, <, =)
        var (left, nextIndex) = ParseTerm(tokens, index);

        if (nextIndex < tokens.Count)
        {
            var token = tokens[nextIndex];
            if (token == ">" || token == ">=" || token == "<" || token == "<=" || token == "=" || token == "==" || token == "!=")
            {
                var opKey = token == "==" ? "=" : token;
                if (!_operators.TryGetValue(opKey, out var op))
                    throw new InvalidOperationException($"Unknown operator: {token}");

                var (right, newIndex) = ParseTerm(tokens, nextIndex + 1);
                left = new BinaryExpression(left, op, right);
                nextIndex = newIndex;
            }
        }

        return (left, nextIndex);
    }

    private (IExpression expression, int nextIndex) ParseTerm(List<string> tokens, int index)
    {
        var token = tokens[index];

        // Check for NOT
        if (token.Equals("NOT", StringComparison.OrdinalIgnoreCase))
        {
            if (!_operators.TryGetValue("NOT", out var op))
                throw new InvalidOperationException("Unknown operator: NOT");

            var (operand, nextIndex) = ParseTerm(tokens, index + 1);
            // For unary NOT, we create a special case
            return (new UnaryExpression(op, operand), nextIndex);
        }

        // Check for function calls
        if (index + 1 < tokens.Count && tokens[index + 1] == "(")
        {
            if (!_functions.TryGetValue(token, out var function))
                throw new InvalidOperationException($"Unknown function: {token}");

            var (args, nextIndex) = ParseFunctionArguments(tokens, index + 2);
            var expression = (IExpression)new FunctionCallExpression(function, args.ToArray());

            // Check for field access (e.g., .signal, .histogram)
            if (nextIndex < tokens.Count && tokens[nextIndex] == ".")
            {
                if (nextIndex + 1 >= tokens.Count)
                    throw new InvalidOperationException("Expected field name after '.'");

                var fieldName = tokens[nextIndex + 1];
                expression = new FieldAccessExpression(expression, fieldName);
                nextIndex += 2;
            }

            return (expression, nextIndex);
        }

        // Check for numbers
        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return (new LiteralExpression(number), index + 1);
        }

        // Check for data access literals (close, open, high, low, vwap, volume, float)
        if (!string.IsNullOrEmpty(token) && char.IsLetter(token[0]))
        {
            var lowerToken = token.ToLowerInvariant();
            if (IsDataAccessKeyword(lowerToken))
            {
                return (new DataAccessExpression(lowerToken), index + 1);
            }
            else
            {
                return (new LiteralExpression(token), index + 1);
            }
        }

        throw new InvalidOperationException($"Unexpected token: {token}");
    }

    private (List<IExpression> arguments, int nextIndex) ParseFunctionArguments(List<string> tokens, int index)
    {
        var args = new List<IExpression>();

        while (index < tokens.Count && tokens[index] != ")")
        {
            var (arg, nextIndex) = ParseExpression(tokens, index);
            args.Add(arg);
            index = nextIndex;

            if (index < tokens.Count && tokens[index] == ",")
            {
                index++;
            }
        }

        if (index >= tokens.Count || tokens[index] != ")")
            throw new InvalidOperationException("Expected closing parenthesis");

        return (args, index + 1);
    }

    private static bool IsDataAccessKeyword(string token)
    {
        return token switch
        {
            "close" or "open" or "high" or "low" or "vwap" or "volume" or "float" => true,
            _ => false
        };
    }

    private static bool IsPotentialDecimalSeparator(string script, int index, string currentToken)
    {
        if (index + 1 >= script.Length)
        {
            return false;
        }

        var nextChar = script[index + 1];
        if (!char.IsDigit(nextChar))
        {
            return false;
        }

        if (string.IsNullOrEmpty(currentToken))
        {
            return false;
        }

        return IsNumericToken(currentToken);
    }

    private static bool IsNumericToken(string token)
    {
        int start = token[0] == '-' ? 1 : 0;
        if (start >= token.Length)
        {
            return false;
        }

        for (int i = start; i < token.Length; i++)
        {
            var ch = token[i];
            if (!char.IsDigit(ch))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Helper class for unary expressions (like NOT)
/// </summary>
public class UnaryExpression(IOperator op, IExpression operand) : IExpression
{
    private readonly IOperator _operator = op;
    private readonly IExpression _operand = operand;

    public IOperator Operator => _operator;
    public IExpression Operand => _operand;

    public object Evaluate(ExpressionContext context)
    {
        var operandValue = _operand.Evaluate(context);
        return _operator.Execute(null, operandValue, context);
    }
}
