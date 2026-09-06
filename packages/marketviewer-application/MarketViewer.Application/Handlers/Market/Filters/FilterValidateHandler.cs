using MarketViewer.Application.Services;
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
/// Surfaces the real filter parser to the UI: validation errors, the canonical spelling with
/// display spans for chip rendering, and an English echo. The parser stays the single source of
/// grammar truth; the client never serializes an expression itself (plan 20, decision 5).
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
                var violation = FilterExpressionValidator.GetContextViolation(parsed, required);
                if (violation is not null)
                {
                    return new FilterValidationResult
                    {
                        Expression = expression,
                        Valid = false,
                        Error = violation,
                    };
                }
            }

            var canonical = FilterCanonicalizer.Canonicalize(parsed);
            return new FilterValidationResult
            {
                Expression = expression,
                Valid = true,
                Description = Describe(canonical),
                Canonical = canonical.Text,
                Timeframe = canonical.Timeframe,
                Segments = canonical.Segments
                    .Select(s => new FilterSegment { Role = s.Role, Start = s.Start, End = s.End, Edit = s.Edit })
                    .ToList(),
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

    #region English echo

    private static string Describe(CanonicalFilter canonical)
    {
        var inner = canonical.Root is TimeframeRangeExpression range ? range.GetInnerExpression() : canonical.Root;
        var text = Describe(inner);

        if (canonical.Timeframe is null)
        {
            return text;
        }

        var timeframe = RangeSuffix.FormatTimeframe(canonical.Timeframe);
        if (canonical.Candles is not > 1)
        {
            return $"{text} on the {timeframe} chart";
        }

        var mode = canonical.Mode == RangeEvaluationMode.Any ? "any" : "all";
        text = $"{text} on {mode} of the last {canonical.Candles} {timeframe} candles";
        if (canonical.Mode == RangeEvaluationMode.All && canonical.HasCross)
        {
            // Mixed line: "all" governs the comparisons; a cross fires on any candle in the window.
            text += " (the cross on any of them)";
        }
        return text;
    }

    private static string Describe(IExpression node) => node switch
    {
        TimeframeRangeExpression range => Describe(range.GetInnerExpression()),
        BinaryExpression { Operator: ILogicalOperator } logical =>
            $"{DescribeOperand(logical.Left, logical.Operator.Symbol)} {logical.Operator.Symbol.ToLowerInvariant()} {DescribeOperand(logical.Right, logical.Operator.Symbol)}",
        UnaryExpression unary => $"not {(IsLogical(unary.Operand) ? $"({Describe(unary.Operand)})" : Describe(unary.Operand))}",
        BinaryExpression comparison => $"{Describe(comparison.Left)} is {OpPhrase(comparison.Operator.Symbol)} {Describe(comparison.Right)}",
        FunctionCallExpression { FunctionName: "crosses_over" } cross when cross.GetArguments().Count == 2 =>
            $"{Describe(cross.GetArguments()[0])} crosses above {Describe(cross.GetArguments()[1])}",
        FunctionCallExpression { FunctionName: "crosses_under" } cross when cross.GetArguments().Count == 2 =>
            $"{Describe(cross.GetArguments()[0])} crosses below {Describe(cross.GetArguments()[1])}",
        FunctionCallExpression function => FilterCanonicalizer.PrintValue(function),
        FieldAccessExpression field => $"{Describe(field.GetTargetExpression())} {field.GetFieldName().ToLowerInvariant()}",
        DataAccessExpression data => data.GetFieldName(),
        LiteralExpression literal => FilterCanonicalizer.PrintValue(literal),
        _ => node.ToString() ?? "",
    };

    /// <summary>
    /// A logical operand that is itself a logical expression with a different operator was
    /// grouped by the user: keep the parentheses in the echo so "a and (b or c)" reads as written.
    /// </summary>
    private static string DescribeOperand(IExpression operand, string parentOp) =>
        operand is BinaryExpression { Operator: ILogicalOperator } child
        && !string.Equals(child.Operator.Symbol, parentOp, StringComparison.OrdinalIgnoreCase)
            ? $"({Describe(operand)})"
            : Describe(operand);

    private static bool IsLogical(IExpression node) => node is BinaryExpression { Operator: ILogicalOperator };

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

    #endregion

    #region Contexts

    private static Dictionary<FilterContext, string> ContextNames => FilterExpressionValidator.ContextNames;

    public static bool TryParseContext(string value, out FilterContext context) =>
        FilterExpressionValidator.TryParseContext(value, out context);

    private static List<string> ContextList(FilterContext contexts) =>
        FilterExpressionValidator.ContextList(contexts);

    #endregion

    #region Autocomplete metadata

    /// <summary>
    /// The catalog is derived from <see cref="FunctionRegistry"/> ([FilterFunction] attributes +
    /// KeywordRegistry) plus one pseudo-entry for the "[timeframe, candles, mode]" line suffix
    /// (<see cref="RangeSuffix"/>), so the composer's bracket hint is driven by the same definition the
    /// parser uses. There is no separate list to keep in sync. Optionally filtered to one context.
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
            .Append(SuffixInfo())
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

    /// <summary>The bracket suffix as a catalog entry: valid in every context, documented on the reference index.</summary>
    public static FilterFunctionInfo SuffixInfo() => new()
    {
        Kind = "suffix",
        Name = RangeSuffix.CatalogName,
        Signature = RangeSuffix.Signature,
        Snippet = RangeSuffix.Snippet,
        Description = RangeSuffix.Description,
        Params = RangeSuffix.SlotNames.ToList(),
        ParamOptions = new Dictionary<string, List<string>>
        {
            ["timeframe"] = RangeSuffix.TimeframeOptions.ToList(),
            ["mode"] = RangeSuffix.ModeOptions.ToList(),
        },
        Contexts = ContextList(FilterContext.All),
        DocsUrl = "/docs/filters",
    };

    #endregion
}
