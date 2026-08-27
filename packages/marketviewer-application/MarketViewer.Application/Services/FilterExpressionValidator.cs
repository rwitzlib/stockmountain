using MarketViewer.Filters;
using MarketViewer.Filters.Expressions;
using MarketViewer.Filters.Interfaces;
using MarketViewer.Filters.Parsing;
using MarketViewer.Filters.Registry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MarketViewer.Application.Services;

/// <summary>
/// Shared parse + context checking for filter expressions. The parser stays the single
/// source of grammar truth: used by the /filters/validate endpoint and by the
/// scanner/strategy/backtest create+update validators so a bad expression can never be
/// persisted.
/// </summary>
public static class FilterExpressionValidator
{
    private static readonly IndicatorExpressionEngine Engine = new();

    public static readonly Dictionary<FilterContext, string> ContextNames = new()
    {
        [FilterContext.Scan] = "scan",
        [FilterContext.Backtest] = "backtest",
        [FilterContext.Chart] = "chart",
    };

    public static bool TryParseContext(string value, out FilterContext context)
    {
        foreach (var (flag, name) in ContextNames)
        {
            if (string.Equals(name, value, StringComparison.OrdinalIgnoreCase))
            {
                context = flag;
                return true;
            }
        }
        context = FilterContext.None;
        return false;
    }

    public static List<string> ContextList(FilterContext contexts) =>
        ContextNames.Where(kv => contexts.HasFlag(kv.Key)).Select(kv => kv.Value).ToList();

    /// <summary>
    /// Parses <paramref name="expression"/> and checks it against <paramref name="context"/>.
    /// Returns null when valid, otherwise the error message (the parser's own message, or
    /// the context-violation message).
    /// </summary>
    public static string? GetError(string expression, FilterContext context)
    {
        try
        {
            var parsed = Engine.ParseExpression(expression);
            return GetContextViolation(parsed, context);
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// Returns the context-violation message for an already-parsed expression, or null
    /// when every function/keyword is allowed in <paramref name="context"/>.
    /// </summary>
    public static string? GetContextViolation(IExpression expression, FilterContext context)
    {
        var offender = FindContextViolation(expression, context);
        return offender is null
            ? null
            : $"'{offender.Name}' is not available in {ContextNames[context]} filters (valid in: {string.Join(", ", ContextList(offender.Contexts))}).";
    }

    /// <summary>
    /// Walks the parsed expression and returns the first function/keyword descriptor that is not
    /// declared for <paramref name="context"/>, or null when everything is allowed.
    /// </summary>
    private static FunctionDescriptor? FindContextViolation(IExpression expression, FilterContext context)
    {
        switch (expression)
        {
            case TimeframeRangeExpression range:
                return FindContextViolation(range.GetInnerExpression(), context);
            case BinaryExpression binary:
                return FindContextViolation(binary.Left, context) ?? FindContextViolation(binary.Right, context);
            case UnaryExpression unary:
                return FindContextViolation(unary.Operand, context);
            case FieldAccessExpression field:
                return FindContextViolation(field.GetTargetExpression(), context);
            case FunctionCallExpression function:
                if (FunctionRegistry.TryGetFunction(function.FunctionName, out var fd) && !fd.SupportsContext(context))
                    return fd;
                foreach (var arg in function.GetArguments())
                {
                    var inner = FindContextViolation(arg, context);
                    if (inner is not null) return inner;
                }
                return null;
            case DataAccessExpression data:
                return KeywordRegistry.TryGet(data.GetFieldName(), out var kd) && !kd.SupportsContext(context) ? kd : null;
            default:
                return null;
        }
    }
}
