using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Requests.Market.Filters;
using MarketViewer.Contracts.Responses.Market.Filters;
using MarketViewer.Filters;
using MarketViewer.Filters.Expressions;
using MarketViewer.Filters.Interfaces;
using MarketViewer.Filters.Parsing;
using MarketViewer.Filters.Registry;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace MarketViewer.Application.Handlers.Market.Filters;

/// <summary>
/// Surfaces the real filter parser to the UI: validation errors, a presentation AST for
/// chip rendering, and an English echo. The parser stays the single source of grammar truth.
/// </summary>
public class FilterValidateHandler(ILogger<FilterValidateHandler> logger)
{
    private readonly IndicatorExpressionEngine _engine = new();

    public OperationResult<FilterValidateResponse> Validate(FilterValidateRequest request)
    {
        if (request.Expressions is not { Count: > 0 })
        {
            return new OperationResult<FilterValidateResponse>
            {
                Status = HttpStatusCode.BadRequest,
                ErrorMessages = ["At least one expression is required."]
            };
        }

        FilterContext? context = null;
        if (!string.IsNullOrWhiteSpace(request.Context))
        {
            if (!TryParseContext(request.Context, out var parsedContext))
            {
                return new OperationResult<FilterValidateResponse>
                {
                    Status = HttpStatusCode.BadRequest,
                    ErrorMessages = [$"Unknown context '{request.Context}'. Expected one of: {string.Join(", ", ContextNames.Values)}."]
                };
            }
            context = parsedContext;
        }

        var results = request.Expressions.Select(e => ValidateOne(e, context)).ToList();

        return new OperationResult<FilterValidateResponse>
        {
            Status = HttpStatusCode.OK,
            Data = new FilterValidateResponse { Results = results }
        };
    }

    private FilterValidationResult ValidateOne(string expression, FilterContext? context)
    {
        try
        {
            var parsed = _engine.ParseExpression(expression);
            if (context is { } required)
            {
                var offender = FindContextViolation(parsed, required);
                if (offender is not null)
                {
                    return new FilterValidationResult
                    {
                        Expression = expression,
                        Valid = false,
                        Error = $"'{offender.Name}' is not available in {ContextNames[required]} filters (valid in: {string.Join(", ", ContextList(offender.Contexts))}).",
                    };
                }
            }
            var ast = MapNode(parsed);
            return new FilterValidationResult
            {
                Expression = expression,
                Valid = true,
                Description = Describe(ast),
                Timeframe = _engine.ExtractTimeframe(parsed),
                Ast = ast,
            };
        }
        catch (Exception e)
        {
            logger.LogDebug("Filter expression failed to parse: {expression} ({error})", expression, e.Message);
            return new FilterValidationResult
            {
                Expression = expression,
                Valid = false,
                Error = e.Message,
            };
        }
    }

    #region AST mapping

    private static FilterAstNode MapNode(IExpression expression) => expression switch
    {
        TimeframeRangeExpression range => new FilterAstNode
        {
            Kind = "range",
            Inner = MapNode(range.GetInnerExpression()),
            Timeframe = range.GetTimeframe(),
            Candles = range.GetRange(),
        },
        BinaryExpression binary => new FilterAstNode
        {
            Kind = "binary",
            Op = binary.Operator.Symbol,
            Left = MapNode(binary.Left),
            Right = MapNode(binary.Right),
        },
        UnaryExpression unary => new FilterAstNode
        {
            Kind = "unary",
            Op = unary.Operator.Symbol,
            Inner = MapNode(unary.Operand),
        },
        FunctionCallExpression function => new FilterAstNode
        {
            Kind = "function",
            Name = function.FunctionName,
            Args = function.GetArguments().Select(MapNode).ToList(),
        },
        FieldAccessExpression field => new FilterAstNode
        {
            Kind = "field",
            Target = MapNode(field.GetTargetExpression()),
            Field = field.GetFieldName(),
        },
        DataAccessExpression data => new FilterAstNode
        {
            Kind = "data",
            Field = data.GetFieldName(),
        },
        LiteralExpression literal => new FilterAstNode
        {
            Kind = "literal",
            Value = FormatLiteral(literal.GetValue()),
        },
        _ => new FilterAstNode
        {
            Kind = "raw",
            Value = expression.ToString(),
        },
    };

    private static string FormatLiteral(object? value) => value switch
    {
        null => "",
        IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    #endregion

    #region English echo

    private static string Describe(FilterAstNode node) => node.Kind switch
    {
        "range" => DescribeRange(node),
        "binary" when IsLogical(node.Op) =>
            $"{DescribeOperand(node.Left!, node.Op!)} {node.Op!.ToLowerInvariant()} {DescribeOperand(node.Right!, node.Op!)}",
        "unary" => $"not {(IsLogicalNode(node.Inner!) ? $"({Describe(node.Inner!)})" : Describe(node.Inner!))}",
        "binary" => $"{Describe(node.Left!)} is {OpPhrase(node.Op!)} {Describe(node.Right!)}",
        "function" when node.Name is "crosses_over" && node.Args is { Count: 2 } =>
            $"{Describe(node.Args[0])} crosses above {Describe(node.Args[1])}",
        "function" when node.Name is "crosses_under" && node.Args is { Count: 2 } =>
            $"{Describe(node.Args[0])} crosses below {Describe(node.Args[1])}",
        "function" => $"{node.Name}({string.Join(", ", (node.Args ?? []).Select(Describe))})",
        "field" => $"{Describe(node.Target!)} {node.Field}",
        "data" => node.Field ?? "",
        "literal" => node.Value ?? "",
        _ => node.Value ?? "",
    };

    /// <summary>
    /// A logical operand that is itself a logical expression with a different operator was
    /// grouped by the user — keep the parentheses in the echo so "a and (b or c)" reads as written.
    /// </summary>
    private static string DescribeOperand(FilterAstNode operand, string parentOp) =>
        IsLogicalNode(operand) && !string.Equals(operand.Op, parentOp, StringComparison.OrdinalIgnoreCase)
            ? $"({Describe(operand)})"
            : Describe(operand);

    private static bool IsLogicalNode(FilterAstNode node) =>
        node.Kind == "binary" && IsLogical(node.Op);

    private static string DescribeRange(FilterAstNode node)
    {
        var text = Describe(node.Inner!);
        if (node.Timeframe is not null)
        {
            text += $" on the {FormatTimeframe(node.Timeframe)} chart";
        }
        if (node.Candles is > 1)
        {
            text += $" over the last {node.Candles} candles";
        }
        return text;
    }

    private static bool IsLogical(string? op) =>
        op is not null && (op.Equals("AND", StringComparison.OrdinalIgnoreCase) || op.Equals("OR", StringComparison.OrdinalIgnoreCase));

    private static string OpPhrase(string op) => op switch
    {
        ">" => "above",
        "<" => "below",
        ">=" => "at or above",
        "<=" => "at or below",
        "=" or "==" => "equal to",
        "!=" => "not equal to",
        _ => op,
    };

    private static string FormatTimeframe(Timeframe timeframe)
    {
        var unit = timeframe.Timespan switch
        {
            Timespan.minute => "m",
            Timespan.hour => "h",
            Timespan.day => "d",
            Timespan.week => "w",
            Timespan.month => "mo",
            Timespan.quarter => "q",
            Timespan.year => "y",
            _ => timeframe.Timespan.ToString(),
        };
        return $"{timeframe.Multiplier}{unit}";
    }

    #endregion

    #region Contexts

    private static readonly Dictionary<FilterContext, string> ContextNames = new()
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

    private static List<string> ContextList(FilterContext contexts) =>
        ContextNames.Where(kv => contexts.HasFlag(kv.Key)).Select(kv => kv.Value).ToList();

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

    #endregion

    #region Autocomplete metadata

    /// <summary>
    /// The catalog is derived from <see cref="FunctionRegistry"/> ([FilterFunction] attributes +
    /// KeywordRegistry) — there is no separate list to keep in sync. Optionally filtered to one context.
    /// </summary>
    public OperationResult<FilterFunctionsResponse> Functions(string? context = null)
    {
        FilterContext? required = null;
        if (!string.IsNullOrWhiteSpace(context))
        {
            if (!TryParseContext(context, out var parsed))
            {
                return new OperationResult<FilterFunctionsResponse>
                {
                    Status = HttpStatusCode.BadRequest,
                    ErrorMessages = [$"Unknown context '{context}'. Expected one of: {string.Join(", ", ContextNames.Values)}."]
                };
            }
            required = parsed;
        }

        var functions = FunctionRegistry.All
            .Where(d => required is null || d.SupportsContext(required.Value))
            .Select(ToInfo)
            .ToList();

        return new()
        {
            Status = HttpStatusCode.OK,
            Data = new FilterFunctionsResponse { Functions = functions },
        };
    }

    public static FilterFunctionInfo ToInfo(FunctionDescriptor d) => new()
    {
        Kind = d.IsKeyword ? "literal" : "function",
        FunctionKind = d.Kind.ToString().ToLowerInvariant(),
        Name = d.Name,
        Signature = d.Signature,
        Snippet = d.Snippet,
        Description = d.Description,
        Params = d.Params.Count > 0 ? d.Params.ToList() : null,
        Fields = d.Fields.Count > 0 ? d.Fields.ToList() : null,
        Aliases = d.Aliases.Count > 0 ? d.Aliases.ToList() : null,
        Contexts = ContextList(d.Contexts),
        DocsUrl = $"/docs/filters/{d.Name}",
    };

    #endregion
}
