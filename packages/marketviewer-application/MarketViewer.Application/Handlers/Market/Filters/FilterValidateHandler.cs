using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Requests.Market.Filters;
using MarketViewer.Contracts.Responses.Market.Filters;
using MarketViewer.Filters;
using MarketViewer.Filters.Expressions;
using MarketViewer.Filters.Interfaces;
using MarketViewer.Filters.Parsing;
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

        var results = request.Expressions.Select(ValidateOne).ToList();

        return new OperationResult<FilterValidateResponse>
        {
            Status = HttpStatusCode.OK,
            Data = new FilterValidateResponse { Results = results }
        };
    }

    private FilterValidationResult ValidateOne(string expression)
    {
        try
        {
            var parsed = _engine.ParseExpression(expression);
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

    #region Autocomplete metadata

    /// <summary>
    /// Static metadata for the smart input. Update alongside parser additions (plan 11);
    /// this list and the Describe formatter are the only UI-facing registries.
    /// </summary>
    private static readonly List<FilterFunctionInfo> FunctionCatalog =
    [
        new() { Kind = "literal", Name = "close", Signature = "close", Snippet = "close", Description = "Closing price series" },
        new() { Kind = "literal", Name = "open", Signature = "open", Snippet = "open", Description = "Opening price series" },
        new() { Kind = "literal", Name = "high", Signature = "high", Snippet = "high", Description = "High price series" },
        new() { Kind = "literal", Name = "low", Signature = "low", Snippet = "low", Description = "Low price series" },
        new() { Kind = "literal", Name = "volume", Signature = "volume", Snippet = "volume", Description = "Volume series" },
        new() { Kind = "literal", Name = "float", Signature = "float", Snippet = "float", Description = "Ticker share float (scalar; fails comparison when unavailable)" },
        new() { Kind = "literal", Name = "time", Signature = "time", Snippet = "time", Description = "Candle time of day, Eastern. Compare against HH:MM, or use .hour / .minute", Fields = ["hour", "minute"] },
        new() { Kind = "function", Name = "sma", Signature = "sma(period)", Snippet = "sma(14)", Description = "Simple moving average", Params = ["period"] },
        new() { Kind = "function", Name = "ema", Signature = "ema(period)", Snippet = "ema(14)", Description = "Exponential moving average", Params = ["period"] },
        new() { Kind = "function", Name = "rsi", Signature = "rsi(period, overbought, oversold, type)", Snippet = "rsi(14,70,30,wilders)", Description = "Relative Strength Index (0–100). All 4 args required; overbought/oversold are informational only. Type: wilders / ema / sma", Params = ["period", "overbought", "oversold", "type"] },
        new() { Kind = "function", Name = "macd", Signature = "macd(fast, slow, signal, type)", Snippet = "macd(12,26,9,ema)", Description = "MACD; fields: value, signal, histogram", Params = ["fast", "slow", "signal", "type"], Fields = ["value", "macd", "signal", "histogram"] },
        new() { Kind = "function", Name = "vwap", Signature = "vwap([anchor])", Snippet = "vwap()", Description = "Session VWAP anchored at 09:30 ET (no value pre-market); vwap(day) anchors at the Eastern date change to include pre-market", Params = ["anchor?"] },
        new() { Kind = "function", Name = "adv", Signature = "adv([period])", Snippet = "adv()", Description = "Average daily volume; optional lookback period", Params = ["period?"] },
        new() { Kind = "function", Name = "slope", Signature = "slope(series[, period])", Snippet = "slope(close, 5)", Description = "Linear regression slope over a rolling window (default 5)", Params = ["series", "period?"] },
        new() { Kind = "function", Name = "crosses_over", Signature = "crosses_over(series1, series2)", Snippet = "crosses_over(close, sma(20))", Description = "True when series1 crosses above series2", Params = ["series1", "series2"] },
        new() { Kind = "function", Name = "crosses_under", Signature = "crosses_under(series1, series2)", Snippet = "crosses_under(close, sma(20))", Description = "True when series1 crosses below series2", Params = ["series1", "series2"] },
        new()
        {
            Kind = "function",
            Name = "support_resistance",
            Signature = "support_resistance(lookback, swing, cluster%, atrMult, atrPeriod, minTouches)",
            Snippet = "support_resistance()",
            Description = "Support/resistance zone mapper (alias: sr). Positive value = closer to resistance",
            Params = ["lookback?", "swing?", "cluster%?", "atrMult?", "atrPeriod?", "minTouches?"],
            Fields =
            [
                "value", "support", "resistance", "support_strength", "resistance_strength",
                "support_distance", "resistance_distance", "support_distance_pct", "resistance_distance_pct",
                "support_zone_width", "resistance_zone_width", "support_touches", "resistance_touches",
                "support_upper", "support_lower", "resistance_upper", "resistance_lower",
                "near_support", "near_resistance",
            ],
        },
    ];

    public OperationResult<FilterFunctionsResponse> Functions() => new()
    {
        Status = HttpStatusCode.OK,
        Data = new FilterFunctionsResponse { Functions = FunctionCatalog },
    };

    #endregion
}
