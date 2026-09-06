using System.Globalization;
using System.Text;
using MarketViewer.Contracts.Models;
using MarketViewer.Filters.Expressions;
using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Parsing;

/// <summary>
/// One display piece of a canonical filter: a character span into <see cref="CanonicalFilter.Text"/>,
/// a rendering role, and (for leaves the user may quick-edit) what kind of edit applies. Text between
/// segments (spaces, parentheses, commas) is punctuation and is not editable.
/// </summary>
/// <param name="Role">function | data | literal | op | logic | timeframe</param>
/// <param name="Edit">null | value | op | timeframe | candles | mode</param>
public sealed record CanonicalSegment(string Role, int Start, int End, string? Edit)
{
    public int Length => End - Start;
}

/// <summary>
/// The canonical form of a filter line (plan 20, decisions 3 and 5): deterministic text, the
/// normalized tree it was printed from (re-parsing <see cref="Text"/> yields the same tree), and
/// spans for chip rendering. A UI edit is a text splice on a segment followed by re-validation;
/// nothing outside the parser ever serializes an expression.
/// </summary>
public sealed class CanonicalFilter
{
    public required string Text { get; init; }
    public required IExpression Root { get; init; }
    public required IReadOnlyList<CanonicalSegment> Segments { get; init; }

    /// <summary>Explicit on every series line (default 1m); null only for scalar-only lines.</summary>
    public Timeframe? Timeframe { get; init; }

    /// <summary>Candle window when more than one candle is examined; null for a single candle.</summary>
    public int? Candles { get; init; }

    /// <summary>Set whenever <see cref="Candles"/> is set: <c>all</c> for comparisons, <c>any</c> for cross-only lines.</summary>
    public RangeEvaluationMode? Mode { get; init; }

    public bool IsScalarOnly { get; init; }
    public bool IsCrossOnly { get; init; }
    public bool HasCross { get; init; }
    public bool HasComparison { get; init; }

    /// <summary>The text covered by a segment.</summary>
    public string Slice(CanonicalSegment segment) => Text.Substring(segment.Start, segment.Length);
}

/// <summary>
/// Canonical printer for parsed filters. Rules (plan 20, decision 3):
/// <list type="bullet">
/// <item>Series lines always carry an explicit timeframe: <c>close &gt; sma(20)</c> prints as <c>close &gt; sma(20) [1m]</c>.</item>
/// <item>The mode is always written when more than one candle is examined: <c>[1m, 5]</c> prints as <c>[1m, 5, all]</c>; cross-only lines print <c>any</c>.</item>
/// <item>A candle count of 1 is redundant and dropped: <c>[1m, 1]</c> prints as <c>[1m]</c>.</item>
/// <item>Scalar-only lines (<c>float &gt; 1000000</c>) stay bare, so a bare line reliably means a per-ticker filter.</item>
/// <item>Logical operands that are themselves logical expressions keep parentheses when their operator differs
/// from the parent's, or when they are the right operand of the same operator (so the tree round-trips exactly).</item>
/// <item>Function arguments are separated by <c>", "</c>; operators are surrounded by single spaces; keywords and
/// field names are lower-case; aliases print as the registered name; <c>==</c> prints as <c>=</c>.</item>
/// </list>
/// </summary>
public static class FilterCanonicalizer
{
    public static CanonicalFilter Canonicalize(IExpression parsed)
    {
        // The parser only ever wraps the whole line.
        var inner = parsed;
        Timeframe? timeframe = null;
        int? candles = null;
        RangeEvaluationMode? mode = null;

        while (inner is TimeframeRangeExpression range)
        {
            timeframe ??= range.GetTimeframe();
            candles ??= range.GetRange();
            mode ??= range.GetRangeEvaluationMode();
            inner = range.GetInnerExpression();
        }

        var isScalarOnly = ExpressionShape.IsScalarOnly(inner);
        var isCrossOnly = ExpressionShape.IsCrossOnly(inner);
        var hasCross = ExpressionShape.HasBooleanFunction(inner);
        var hasComparison = ExpressionShape.HasComparison(inner);

        IExpression root;
        if (isScalarOnly)
        {
            timeframe = null;
            candles = null;
            mode = null;
            root = inner;
        }
        else
        {
            timeframe ??= RangeSuffix.DefaultTimeframe;
            if (candles is not > 1)
            {
                candles = null;
                mode = null;
            }
            else
            {
                mode ??= isCrossOnly ? RangeEvaluationMode.Any : RangeEvaluationMode.All;
            }
            root = new TimeframeRangeExpression(inner, timeframe, candles, mode);
        }

        var printer = new Printer();
        printer.PrintLine(inner, timeframe, candles, mode);

        return new CanonicalFilter
        {
            Text = printer.Text,
            Root = root,
            Segments = printer.Segments,
            Timeframe = timeframe,
            Candles = candles,
            Mode = mode,
            IsScalarOnly = isScalarOnly,
            IsCrossOnly = isCrossOnly,
            HasCross = hasCross,
            HasComparison = hasComparison,
        };
    }

    /// <summary>Canonical text only.</summary>
    public static string Print(IExpression parsed) => Canonicalize(parsed).Text;

    /// <summary>Canonical text of a value expression (literal, keyword, function call, field access).</summary>
    public static string PrintValue(IExpression expression) => Printer.Operand(expression);

    private sealed class Printer
    {
        private readonly StringBuilder _sb = new();
        private readonly List<CanonicalSegment> _segments = [];

        public string Text => _sb.ToString();
        public IReadOnlyList<CanonicalSegment> Segments => _segments;

        public void PrintLine(IExpression inner, Timeframe? timeframe, int? candles, RangeEvaluationMode? mode)
        {
            PrintCondition(inner);

            if (timeframe is null)
            {
                return;
            }

            _sb.Append(" [");
            Segment("timeframe", RangeSuffix.FormatTimeframe(timeframe), "timeframe");
            if (candles is not null)
            {
                _sb.Append(", ");
                Segment("timeframe", candles.Value.ToString(CultureInfo.InvariantCulture), "candles");
                if (mode is not null)
                {
                    _sb.Append(", ");
                    Segment("timeframe", RangeSuffix.FormatMode(mode.Value), "mode");
                }
            }
            _sb.Append(']');
        }

        /// <summary>A boolean-valued node: logical combination, NOT, comparison, or a boolean function call.</summary>
        private void PrintCondition(IExpression expression)
        {
            switch (expression)
            {
                case BinaryExpression { Operator: ILogicalOperator } logical:
                    PrintLogicalOperand(logical.Left, logical, isRight: false);
                    _sb.Append(' ');
                    Segment("logic", logical.Operator.Symbol.ToUpperInvariant(), null);
                    _sb.Append(' ');
                    PrintLogicalOperand(logical.Right, logical, isRight: true);
                    return;

                case UnaryExpression unary:
                    Segment("logic", unary.Operator.Symbol.ToUpperInvariant(), null);
                    _sb.Append(' ');
                    if (IsLogical(unary.Operand))
                    {
                        _sb.Append('(');
                        PrintCondition(unary.Operand);
                        _sb.Append(')');
                    }
                    else
                    {
                        PrintCondition(unary.Operand);
                    }
                    return;

                case BinaryExpression comparison:
                    PrintComparisonOperand(comparison.Left);
                    _sb.Append(' ');
                    Segment("op", comparison.Operator.Symbol, "op");
                    _sb.Append(' ');
                    PrintComparisonOperand(comparison.Right);
                    return;

                default:
                    PrintComparisonOperand(expression);
                    return;
            }
        }

        private void PrintLogicalOperand(IExpression operand, BinaryExpression parent, bool isRight)
        {
            var group = operand is BinaryExpression { Operator: ILogicalOperator } child
                && (isRight || !string.Equals(child.Operator.Symbol, parent.Operator.Symbol, StringComparison.OrdinalIgnoreCase));

            if (group)
            {
                _sb.Append('(');
                PrintCondition(operand);
                _sb.Append(')');
            }
            else
            {
                PrintCondition(operand);
            }
        }

        /// <summary>A value-valued node: literal, keyword, function call, field access; or a grouped condition.</summary>
        private void PrintComparisonOperand(IExpression operand)
        {
            switch (operand)
            {
                case LiteralExpression literal:
                    Segment("literal", FormatLiteral(literal), "value");
                    return;
                case DataAccessExpression data:
                    Segment("data", data.GetFieldName(), null);
                    return;
                case FunctionCallExpression call:
                    Segment("function", Operand(call), null);
                    return;
                case FieldAccessExpression field:
                    Segment(field.GetTargetExpression() is DataAccessExpression ? "data" : "function", Operand(field), null);
                    return;
                case BinaryExpression or UnaryExpression:
                    // A grouped condition used as a value, e.g. "(close > 1) > 0". Legal, so keep it legal.
                    _sb.Append('(');
                    PrintCondition(operand);
                    _sb.Append(')');
                    return;
                default:
                    throw new InvalidOperationException($"Cannot print expression of type {operand.GetType().Name}");
            }
        }

        private void Segment(string role, string text, string? edit)
        {
            var start = _sb.Length;
            _sb.Append(text);
            _segments.Add(new CanonicalSegment(role, start, _sb.Length, edit));
        }

        private static bool IsLogical(IExpression expression) => expression is BinaryExpression { Operator: ILogicalOperator };

        /// <summary>Text of a value expression with no segments of its own (function calls are one chip).</summary>
        internal static string Operand(IExpression expression) => expression switch
        {
            LiteralExpression literal => FormatLiteral(literal),
            DataAccessExpression data => data.GetFieldName(),
            FunctionCallExpression call => $"{call.FunctionName}({string.Join(", ", call.GetArguments().Select(Operand))})",
            FieldAccessExpression field => $"{Operand(field.GetTargetExpression())}.{field.GetFieldName().ToLowerInvariant()}",
            BinaryExpression or UnaryExpression => $"({new Printer().Condition(expression)})",
            _ => throw new InvalidOperationException($"Cannot print expression of type {expression.GetType().Name}"),
        };

        private string Condition(IExpression expression)
        {
            PrintCondition(expression);
            return Text;
        }

        private static string FormatLiteral(LiteralExpression literal)
        {
            if (literal.SourceText is { Length: > 0 } source)
            {
                return source;
            }

            return literal.GetValue() switch
            {
                double d => d.ToString("R", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                var other => other?.ToString() ?? "",
            };
        }
    }
}
