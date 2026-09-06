using System;
using System.Globalization;
using MarketViewer.Filters.Expressions;
using MarketViewer.Filters.Interfaces;
using MarketViewer.Filters.Operators.Comparison;
using MarketViewer.Filters.Operators.Logical;
using System.Text.RegularExpressions;
using MarketViewer.Filters.Registry;
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
        // Functions come from [FilterFunction] attributes via reflection (Registry/FunctionRegistry).
        _functions = FunctionRegistry.CreateFunctionMap();

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

    /// <summary>A lexical token and the character offset where it starts in the script.</summary>
    private readonly record struct Token(string Text, int Position);

    public IExpression Parse(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
            throw new ArgumentException("Script cannot be null or empty");

        // Split off the trailing "[timeframe, candles, mode]" suffix, if any.
        var (expressionScript, suffix) = ParseTimeframeAndRange(script.Trim());
        if (expressionScript.IndexOfAny(['[', ']']) >= 0)
        {
            throw new InvalidOperationException("Only one [timeframe, candles, mode] suffix is allowed and it must end the line; it applies to every comparison on the line");
        }

        // Tokenize the expression part
        var tokens = Tokenize(expressionScript);
        if (tokens.Count == 0)
            throw new InvalidOperationException("Expected an expression before the [timeframe, candles, mode] suffix");

        // Parse the expression
        var (expression, consumed) = ParseExpression(tokens, 0);
        if (consumed < tokens.Count)
        {
            var stray = tokens[consumed];
            throw new InvalidOperationException(stray.Text == ")"
                ? $"Unexpected ')' at position {stray.Position}: no matching '('"
                : $"Unexpected token '{stray.Text}' at position {stray.Position}");
        }

        if (suffix is null)
        {
            return expression;
        }

        // Suffix rules that need the parsed line (plan 20, decision 2).
        if (ExpressionShape.IsScalarOnly(expression))
        {
            throw new InvalidOperationException(
                "The [timeframe, candles, mode] suffix does not apply to a line with no bar data (e.g. 'float > 1000000'): it is evaluated once per ticker. Remove the suffix.");
        }

        if (suffix.Mode == RangeEvaluationMode.All && ExpressionShape.IsCrossOnly(expression))
        {
            throw new InvalidOperationException(
                "'all' does not apply to a cross: crosses_over/crosses_under fire when a cross happens on any candle in the range. Use [" +
                $"{RangeSuffix.FormatTimeframe(suffix.Timeframe)}, {suffix.Candles}] or [{RangeSuffix.FormatTimeframe(suffix.Timeframe)}, {suffix.Candles}, any].");
        }

        return new TimeframeRangeExpression(expression, suffix.Timeframe, suffix.Candles, suffix.Mode);
    }

    private sealed record ParsedSuffix(Timeframe Timeframe, int? Candles, RangeEvaluationMode? Mode);

    /// <summary>
    /// Strict positional suffix: <c>[timeframe]</c>, <c>[timeframe, candles]</c> or
    /// <c>[timeframe, candles, mode]</c> at the very end of the line. Every deviation is an error
    /// (never silently reinterpreted), so a typo cannot change what a filter means.
    /// </summary>
    private static (string expression, ParsedSuffix? suffix) ParseTimeframeAndRange(string script)
    {
        var bracketStart = script.LastIndexOf('[');
        var bracketEnd = script.LastIndexOf(']');

        if (bracketStart == -1 && bracketEnd == -1)
        {
            return (script, null);
        }

        if (bracketStart == -1 || bracketEnd == -1 || bracketEnd < bracketStart)
        {
            throw new InvalidOperationException("Unbalanced bracket: expected a [timeframe, candles, mode] suffix like [1m, 5, any]");
        }

        if (bracketEnd != script.Length - 1)
        {
            throw new InvalidOperationException("The [timeframe, candles, mode] suffix must be the last thing on the line");
        }

        var expressionPart = script.Substring(0, bracketStart).Trim();
        var inner = script.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);

        // Keep empty slots so "[, 5]" is reported as a missing timeframe rather than reinterpreted.
        var parts = inner.Split(',', StringSplitOptions.TrimEntries);

        if (parts.Length == 1 && parts[0].Length == 0)
        {
            throw new InvalidOperationException("Empty suffix: expected [timeframe], [timeframe, candles] or [timeframe, candles, mode], e.g. [1m, 5, any]");
        }

        if (parts.Length > 3)
        {
            throw new InvalidOperationException($"Too many items in the suffix '[{inner.Trim()}]': expected at most [timeframe, candles, mode]");
        }

        // Slot 1: timeframe (required).
        var first = parts[0];
        if (first.Length == 0)
        {
            throw new InvalidOperationException("Timeframe is required as the first item in the suffix: [timeframe, candles, mode], e.g. [1m, 5]");
        }

        if (IsInteger(first))
        {
            throw new InvalidOperationException($"Timeframe is required before the candle count: write [1m, {first}] instead of [{first}]");
        }

        if (RangeSuffix.TryParseMode(first, out _))
        {
            throw new InvalidOperationException($"'{first}' is a mode and goes last: [timeframe, candles, mode], e.g. [1m, 5, {first.ToLowerInvariant()}]");
        }

        if (!RangeSuffix.TryParseTimeframe(first, out var timeframe))
        {
            throw new InvalidOperationException($"Unknown timeframe '{first}': expected a quantity and unit such as 1m, 5m, 15m, 1h or 1d");
        }

        int? candles = null;
        RangeEvaluationMode? mode = null;

        // Slot 2: candles (optional, positive integer).
        if (parts.Length >= 2)
        {
            var second = parts[1];
            if (second.Length == 0)
            {
                throw new InvalidOperationException("Empty candle count in the suffix: expected [timeframe, candles, mode], e.g. [1m, 5]");
            }

            if (RangeSuffix.TryParseMode(second, out _))
            {
                throw new InvalidOperationException($"'{second}' needs a candle count before it: [timeframe, candles, mode], e.g. [{RangeSuffix.FormatTimeframe(timeframe)}, 5, {second.ToLowerInvariant()}]");
            }

            if (!IsInteger(second) || !int.TryParse(second, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsedCandles))
            {
                throw new InvalidOperationException($"Expected a candle count after the timeframe, got '{second}': [timeframe, candles, mode], e.g. [{RangeSuffix.FormatTimeframe(timeframe)}, 5]");
            }

            if (parsedCandles < 1)
            {
                throw new InvalidOperationException($"Candle count must be at least 1, got {parsedCandles}");
            }

            candles = parsedCandles;
        }

        // Slot 3: mode (optional, any | all; only meaningful over more than one candle).
        if (parts.Length == 3)
        {
            var third = parts[2];
            if (third.Length == 0)
            {
                throw new InvalidOperationException("Empty mode in the suffix: expected 'any' or 'all' as the third item, e.g. [1m, 5, any]");
            }

            if (!RangeSuffix.TryParseMode(third, out var parsedMode))
            {
                throw new InvalidOperationException($"Expected 'any' or 'all' as the third item in the suffix, got '{third}'");
            }

            if (candles is not > 1)
            {
                throw new InvalidOperationException($"'{third.ToLowerInvariant()}' only applies over more than one candle: use [{RangeSuffix.FormatTimeframe(timeframe)}, 5, {third.ToLowerInvariant()}] or drop the mode");
            }

            mode = parsedMode;
        }

        return (expressionPart, new ParsedSuffix(timeframe, candles, mode));
    }

    private static bool IsInteger(string value) =>
        value.Length > 0 && Regex.IsMatch(value, @"^-?\d+$");

    private static List<Token> Tokenize(string script)
    {
        // Very basic tokenizer - splits on whitespace and operators
        var tokens = new List<Token>();
        var currentToken = "";
        var currentStart = 0;

        void Flush()
        {
            if (!string.IsNullOrEmpty(currentToken))
            {
                tokens.Add(new Token(currentToken, currentStart));
                currentToken = "";
            }
        }

        for (int i = 0; i < script.Length; i++)
        {
            var c = script[i];

            if (char.IsWhiteSpace(c))
            {
                Flush();
            }
            else if (c == '(' || c == ')' || c == ',' || c == '.')
            {
                // Allow decimal points within numeric tokens by checking neighbors.
                if (c == '.' && IsPotentialDecimalSeparator(script, i, currentToken))
                {
                    currentToken += c;
                    continue;
                }

                Flush();
                tokens.Add(new Token(c.ToString(), i));
            }
            else if (c == '!' || c == '>' || c == '<' || c == '=')
            {
                Flush();
                // Two-character operators: !=, >=, <=, ==
                if (i + 1 < script.Length && script[i + 1] == '=')
                {
                    tokens.Add(new Token($"{c}=", i));
                    i++;
                }
                else
                {
                    tokens.Add(new Token(c.ToString(), i));
                }
            }
            else
            {
                if (string.IsNullOrEmpty(currentToken))
                {
                    currentStart = i;
                }
                currentToken += c;
            }
        }

        Flush();

        return tokens;
    }

    private (IExpression expression, int nextIndex) ParseExpression(List<Token> tokens, int index)
    {
        // Logical expressions use standard precedence: NOT binds tightest, then AND, then OR:
        // "a OR b AND c" is "a OR (b AND c)" (SQL/Python rules). Parentheses (ParseTerm) group explicitly.
        return ParseOr(tokens, index);
    }

    private (IExpression expression, int nextIndex) ParseOr(List<Token> tokens, int index)
    {
        var (left, nextIndex) = ParseAnd(tokens, index);

        while (nextIndex < tokens.Count && tokens[nextIndex].Text.Equals("OR", StringComparison.OrdinalIgnoreCase))
        {
            if (!_operators.TryGetValue("OR", out var op))
                throw new InvalidOperationException("Unknown operator: OR");

            var (right, newIndex) = ParseAnd(tokens, nextIndex + 1);
            left = new BinaryExpression(left, op, right);
            nextIndex = newIndex;
        }

        return (left, nextIndex);
    }

    private (IExpression expression, int nextIndex) ParseAnd(List<Token> tokens, int index)
    {
        var (left, nextIndex) = ParseComparison(tokens, index);

        while (nextIndex < tokens.Count && tokens[nextIndex].Text.Equals("AND", StringComparison.OrdinalIgnoreCase))
        {
            if (!_operators.TryGetValue("AND", out var op))
                throw new InvalidOperationException("Unknown operator: AND");

            var (right, newIndex) = ParseComparison(tokens, nextIndex + 1);
            left = new BinaryExpression(left, op, right);
            nextIndex = newIndex;
        }

        return (left, nextIndex);
    }

    private (IExpression expression, int nextIndex) ParseComparison(List<Token> tokens, int index)
    {
        // Unary NOT binds tighter than AND/OR but looser than a comparison:
        // "NOT close > sma(20)" negates the whole comparison, "NOT crosses_over(a, b)" negates the call.
        if (index < tokens.Count && tokens[index].Text.Equals("NOT", StringComparison.OrdinalIgnoreCase))
        {
            if (!_operators.TryGetValue("NOT", out var notOp))
                throw new InvalidOperationException("Unknown operator: NOT");

            var (operand, afterOperand) = ParseComparison(tokens, index + 1);
            return (new UnaryExpression(notOp, operand), afterOperand);
        }

        // Parse comparison expressions (>, <, =)
        var (left, nextIndex) = ParseTerm(tokens, index);

        if (nextIndex < tokens.Count)
        {
            var token = tokens[nextIndex].Text;
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

    private (IExpression expression, int nextIndex) ParseTerm(List<Token> tokens, int index)
    {
        if (index >= tokens.Count)
            throw new InvalidOperationException("Unexpected end of expression");

        var token = tokens[index].Text;

        // Parenthesised group: "(" expression ")". Lets AND/OR be grouped explicitly:
        // "close > sma(20) AND (rsi(14) < 30 OR rsi(14) > 70)", and NOT applied to a
        // whole logical expression: "NOT (a OR b)". Without parentheses AND binds tighter
        // than OR (see ParseExpression).
        if (token == "(")
        {
            var (inner, afterInner) = ParseExpression(tokens, index + 1);
            if (afterInner >= tokens.Count || tokens[afterInner].Text != ")")
                throw new InvalidOperationException("Expected closing parenthesis for grouped expression");
            return (inner, afterInner + 1);
        }

        // Check for function calls
        if (index + 1 < tokens.Count && tokens[index + 1].Text == "(")
        {
            if (!_functions.TryGetValue(token, out var function))
                throw new InvalidOperationException($"Unknown function: {token}");

            var (args, nextIndex) = ParseFunctionArguments(tokens, index + 2);
            var expression = (IExpression)new FunctionCallExpression(function, args.ToArray());

            // Check for field access (e.g., .signal, .histogram)
            if (nextIndex < tokens.Count && tokens[nextIndex].Text == ".")
            {
                if (nextIndex + 1 >= tokens.Count)
                    throw new InvalidOperationException("Expected field name after '.'");

                var fieldName = tokens[nextIndex + 1].Text;
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

        // Check for time-of-day literals (e.g. 9:30, 10:45) -> minutes since midnight
        if (TryParseTimeLiteral(token, out var minutesSinceMidnight))
        {
            return (new LiteralExpression(minutesSinceMidnight, token), index + 1);
        }

        // Check for data access literals (close, open, high, low, volume, float, time)
        if (!string.IsNullOrEmpty(token) && char.IsLetter(token[0]))
        {
            var lowerToken = token.ToLowerInvariant();
            if (IsDataAccessKeyword(lowerToken))
            {
                IExpression expression = new DataAccessExpression(lowerToken);
                var nextIndex = index + 1;

                // Check for field access (e.g., time.hour, time.minute)
                if (nextIndex < tokens.Count && tokens[nextIndex].Text == ".")
                {
                    if (nextIndex + 1 >= tokens.Count)
                        throw new InvalidOperationException("Expected field name after '.'");

                    expression = new FieldAccessExpression(expression, tokens[nextIndex + 1].Text);
                    nextIndex += 2;
                }

                return (expression, nextIndex);
            }
            else
            {
                return (new LiteralExpression(token), index + 1);
            }
        }

        throw new InvalidOperationException($"Unexpected token '{token}' at position {tokens[index].Position}");
    }

    private (List<IExpression> arguments, int nextIndex) ParseFunctionArguments(List<Token> tokens, int index)
    {
        var args = new List<IExpression>();

        while (index < tokens.Count && tokens[index].Text != ")")
        {
            var (arg, nextIndex) = ParseExpression(tokens, index);
            args.Add(arg);
            index = nextIndex;

            if (index < tokens.Count && tokens[index].Text == ",")
            {
                index++;
            }
        }

        if (index >= tokens.Count || tokens[index].Text != ")")
            throw new InvalidOperationException("Expected closing parenthesis");

        return (args, index + 1);
    }

    private static bool IsDataAccessKeyword(string token) => KeywordRegistry.IsKeyword(token);

    private static bool TryParseTimeLiteral(string token, out double minutesSinceMidnight)
    {
        minutesSinceMidnight = 0;

        var match = Regex.Match(token, @"^(?<hour>\d{1,2}):(?<minute>\d{2})$");
        if (!match.Success)
        {
            return false;
        }

        var hour = int.Parse(match.Groups["hour"].Value);
        var minute = int.Parse(match.Groups["minute"].Value);

        if (hour > 23 || minute > 59)
        {
            throw new InvalidOperationException($"Invalid time literal: {token}");
        }

        minutesSinceMidnight = hour * 60 + minute;
        return true;
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
