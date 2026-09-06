using MarketViewer.Application.Handlers.Market.Filters;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Requests.Market.Filters;
using MarketViewer.Contracts.Responses.Market.Filters;
using MarketViewer.Filters;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Linq;
using System.Net;
using Xunit;

namespace MarketViewer.Application.UnitTests.Handlers.Filters;

public class FilterValidateHandlerUnitTests
{
    private readonly FilterValidateHandler _handler = new(NullLogger<FilterValidateHandler>.Instance);

    private FilterValidationResult ValidateOne(string expression)
    {
        var result = _handler.Validate(new FilterValidateRequest { Expressions = [expression] });
        Assert.Equal(HttpStatusCode.OK, result.Status);
        return Assert.Single(result.Data!.Results);
    }

    private static string Slice(FilterValidationResult result, FilterSegment segment) =>
        result.Canonical!.Substring(segment.Start, segment.End - segment.Start);

    [Theory]
    [InlineData("close > vwap()")]
    [InlineData("close > vwap(day) [5m]")]
    [InlineData("rsi(14) < 30 [1m]")]
    [InlineData("macd(12,26,9,ema).histogram > 0 [5m]")]
    [InlineData("crosses_over(close, sma(20))")]
    [InlineData("adv() > 2000000 [1d]")]
    [InlineData("volume > 50000 [1m,5]")]
    [InlineData("volume > 50000 [1m, 5, any]")]
    [InlineData("float < 20000000")]
    [InlineData("close > sma(50) AND (rsi(14) < 30 OR rsi(14) > 70) [1m]")]
    [InlineData("NOT (close > sma(50) OR close > sma(200)) [1d]")]
    public void Validate_KnownGoodExpressions_AreValid(string expression)
    {
        var result = ValidateOne(expression);

        Assert.True(result.Valid, result.Error);
        Assert.False(string.IsNullOrWhiteSpace(result.Canonical));
        Assert.NotNull(result.Segments);
        Assert.NotEmpty(result.Segments!);
        Assert.False(string.IsNullOrWhiteSpace(result.Description));
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData("rsl(14) < 30")]
    [InlineData("close > > 5")]
    [InlineData("close >")]
    [InlineData("")]
    // A per-clause timeframe is not supported (the [tf] suffix applies to the whole expression);
    // the parser used to stop silently at the first "[1d]" and validate only "close > sma(50) [1m]".
    [InlineData("close > sma(50) [1d] AND rsi(14) < 30 [1m]")]
    [InlineData("close > 5 ) AND close > 6")]
    // Strict suffix (plan 20): nothing is reinterpreted.
    [InlineData("rsi(14) < 30 [5]")]
    [InlineData("rsi(14) < 30 [, 5]")]
    [InlineData("rsi(14) < 30 [1m, any]")]
    [InlineData("rsi(14) < 30 [1m, 1, any]")]
    [InlineData("rsi(14) < 30 [any]")]
    [InlineData("float < 20000000 [1m]")]
    [InlineData("crosses_over(close, sma(20)) [1m, 5, all]")]
    public void Validate_BadExpressions_ReturnError(string expression)
    {
        var result = ValidateOne(expression);

        Assert.False(result.Valid);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Null(result.Canonical);
        Assert.Null(result.Segments);
    }

    [Fact]
    public void Validate_MixedBatch_ReportsPerExpression()
    {
        var result = _handler.Validate(new FilterValidateRequest
        {
            Expressions = ["close > vwap()", "rsl(14) < 30"],
        });

        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.Equal(2, result.Data!.Results.Count);
        Assert.True(result.Data.Results[0].Valid);
        Assert.False(result.Data.Results[1].Valid);
    }

    [Fact]
    public void Validate_EmptyList_IsBadRequest()
    {
        var result = _handler.Validate(new FilterValidateRequest { Expressions = [] });

        Assert.Equal(HttpStatusCode.BadRequest, result.Status);
    }

    [Theory]
    [InlineData("close > vwap()", "close > vwap() [1m]")]
    [InlineData("close>sma(20)[5m,3]", "close > sma(20) [5m, 3, all]")]
    [InlineData("rsi(14,70,30,wilders) > 70 [1m, 5, any]", "rsi(14, 70, 30, wilders) > 70 [1m, 5, any]")]
    [InlineData("crosses_over(close, sma(20)) [1m, 5]", "crosses_over(close, sma(20)) [1m, 5, any]")]
    [InlineData("float < 20000000", "float < 20000000")]
    [InlineData("close > 100 [1m, 1]", "close > 100 [1m]")]
    public void Validate_ReturnsCanonicalSpelling(string expression, string canonical)
    {
        var result = ValidateOne(expression);

        Assert.True(result.Valid, result.Error);
        Assert.Equal(expression, result.Expression);
        Assert.Equal(canonical, result.Canonical);
    }

    [Fact]
    public void Validate_RsiFilter_ProducesReadableDescriptionAndTimeframe()
    {
        var result = ValidateOne("rsi(14) < 30 [1m]");

        Assert.Equal("rsi(14) is below 30 on the 1m chart", result.Description);
        Assert.NotNull(result.Timeframe);
        Assert.Equal(1, result.Timeframe!.Multiplier);
        Assert.Equal(Timespan.minute, result.Timeframe.Timespan);
    }

    [Fact]
    public void Validate_BareSeriesLine_DefaultsTo1m()
    {
        var result = ValidateOne("close > vwap()");

        Assert.Equal("close is above vwap() on the 1m chart", result.Description);
        Assert.Equal(1, result.Timeframe!.Multiplier);
        Assert.Equal(Timespan.minute, result.Timeframe.Timespan);
    }

    [Fact]
    public void Validate_ScalarLine_HasNoTimeframe()
    {
        var result = ValidateOne("float < 20000000");

        Assert.Equal("float is below 20000000", result.Description);
        Assert.Null(result.Timeframe);
    }

    [Theory]
    [InlineData("rsi(14) < 30 [5m, 3]", "rsi(14) is below 30 on all of the last 3 5m candles")]
    [InlineData("rsi(14) < 30 [5m, 3, any]", "rsi(14) is below 30 on any of the last 3 5m candles")]
    [InlineData("crosses_over(close, sma(20)) [1m, 5]", "close crosses above sma(20) on any of the last 5 1m candles")]
    [InlineData("close > sma(20) AND crosses_over(close, vwap()) [1m, 5, all]",
        "close is above sma(20) and close crosses above vwap() on all of the last 5 1m candles (the cross on any of them)")]
    public void Validate_DescriptionNamesTheMode(string expression, string description)
    {
        Assert.Equal(description, ValidateOne(expression).Description);
    }

    [Fact]
    public void Validate_CrossesOver_DescribesAsCrossing()
    {
        var result = ValidateOne("crosses_over(close, sma(20))");

        Assert.Equal("close crosses above sma(20) on the 1m chart", result.Description);
    }

    [Fact]
    public void Validate_GroupedLogic_KeepsParenthesesInDescriptionAndCanonical()
    {
        var result = ValidateOne("close > sma(20) AND (rsi(14) < 30 OR rsi(14) > 70) [5m]");

        Assert.True(result.Valid, result.Error);
        Assert.Equal("close is above sma(20) and (rsi(14) is below 30 or rsi(14) is above 70) on the 5m chart", result.Description);
        Assert.Equal("close > sma(20) AND (rsi(14) < 30 OR rsi(14) > 70) [5m]", result.Canonical);
    }

    [Fact]
    public void Validate_SameOperatorChain_HasNoParentheses()
    {
        var result = ValidateOne("close > 1 AND close > 2 AND close > 3");

        Assert.Equal("close is above 1 and close is above 2 and close is above 3 on the 1m chart", result.Description);
        Assert.Equal("close > 1 AND close > 2 AND close > 3 [1m]", result.Canonical);
    }

    [Fact]
    public void Validate_Not_ParenthesisesLogicalOperand()
    {
        var single = ValidateOne("NOT close > sma(20)");
        Assert.True(single.Valid, single.Error);
        Assert.Equal("not close is above sma(20) on the 1m chart", single.Description);
        Assert.Equal("NOT close > sma(20) [1m]", single.Canonical);

        var grouped = ValidateOne("NOT (close > 1 OR close > 2)");
        Assert.True(grouped.Valid, grouped.Error);
        Assert.Equal("not (close is above 1 or close is above 2) on the 1m chart", grouped.Description);
    }

    [Fact]
    public void Validate_UnbalancedParenthesis_IsInvalid()
    {
        var result = ValidateOne("close > 1 AND (close > 2 OR close > 3");

        Assert.False(result.Valid);
        Assert.Contains("parenthesis", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Segments_SpanTheCanonicalText()
    {
        var result = ValidateOne("rsi(14) < 30 [1m, 5, any]");

        var pieces = result.Segments!.Select(s => (s.Role, Slice(result, s), s.Edit)).ToList();
        Assert.Equal(
        [
            ("function", "rsi(14)", null),
            ("op", "<", "op"),
            ("literal", "30", "value"),
            ("timeframe", "1m", "timeframe"),
            ("timeframe", "5", "candles"),
            ("timeframe", "any", "mode"),
        ], pieces);
    }

    [Fact]
    public void Validate_SplicingASegmentAndRevalidating_YieldsTheEditedCanonical()
    {
        var result = ValidateOne("rsi(14) < 30 [1m, 5, any]");
        var mode = result.Segments!.Single(s => s.Edit == "mode");

        var spliced = result.Canonical![..mode.Start] + "all" + result.Canonical[mode.End..];
        var edited = ValidateOne(spliced);

        Assert.True(edited.Valid, edited.Error);
        Assert.Equal("rsi(14) < 30 [1m, 5, all]", edited.Canonical);
    }

    [Fact]
    public void Functions_ReturnsCatalogWithSnippets()
    {
        var result = _handler.Functions();

        Assert.Equal(HttpStatusCode.OK, result.Status);
        var functions = result.Data!.Functions;
        Assert.Contains(functions, f => f.Name == "rsi" && f.Kind == "function" && f.Snippet.Contains("rsi("));
        Assert.Contains(functions, f => f.Name == "close" && f.Kind == "literal");
        Assert.Contains(functions, f => f.Name == "macd" && f.Fields!.Contains("histogram"));
    }

    [Fact]
    public void Functions_IsExactlyTheRegistryPlusTheSuffix()
    {
        var functions = _handler.Functions().Data!.Functions;

        Assert.Equal(
            MarketViewer.Filters.Registry.FunctionRegistry.All.Select(d => d.Name).OrderBy(n => n),
            functions.Where(f => f.Kind != "suffix").Select(f => f.Name).OrderBy(n => n));
        Assert.All(functions.Where(f => f.Kind != "suffix"), f =>
        {
            Assert.NotNull(f.Contexts);
            Assert.NotEmpty(f.Contexts!);
            Assert.Equal($"/docs/filters/{f.Name}", f.DocsUrl);
            Assert.False(string.IsNullOrEmpty(f.FunctionKind));
        });
        var sr = Assert.Single(functions, f => f.Name == "support_resistance");
        Assert.Contains("sr", sr.Aliases!);
    }

    [Fact]
    public void Functions_IncludesTheSuffixPseudoEntry()
    {
        var suffix = Assert.Single(_handler.Functions().Data!.Functions, f => f.Kind == "suffix");

        Assert.Equal(RangeSuffix.CatalogName, suffix.Name);
        Assert.Equal("[timeframe, candles, mode]", suffix.Signature);
        Assert.Equal(["timeframe", "candles?", "mode?"], suffix.Params);
        Assert.Equal(["all", "any"], suffix.ParamOptions!["mode"]);
        Assert.Contains("1m", suffix.ParamOptions["timeframe"]);
        Assert.Equal(["scan", "backtest", "chart"], suffix.Contexts);
        Assert.Equal("/docs/filters", suffix.DocsUrl);

        // present in every context
        Assert.Single(_handler.Functions("chart").Data!.Functions, f => f.Kind == "suffix");
    }

    [Fact]
    public void Functions_ChartContext_OnlyChartable()
    {
        var chart = _handler.Functions("chart").Data!.Functions.Where(f => f.Kind != "suffix").ToList();
        Assert.Contains(chart, f => f.Name == "sma");
        Assert.DoesNotContain(chart, f => f.Name == "float");
        Assert.DoesNotContain(chart, f => f.Name == "crosses_over");
        Assert.All(chart, f => Assert.Contains("chart", f.Contexts!));
    }

    [Fact]
    public void Functions_UnknownContext_IsBadRequest()
    {
        Assert.Equal(HttpStatusCode.BadRequest, _handler.Functions("bogus").Status);
    }

    [Theory]
    [InlineData("scan", "float < 20000000", true)]
    [InlineData("backtest", "time < 10:30 AND close > vwap()", true)]
    [InlineData("chart", "sma(20) > 0", true)]
    [InlineData("chart", "float < 20000000", false)]
    [InlineData("chart", "crosses_over(close, sma(20))", false)]
    [InlineData("chart", "close > sma(20) AND slope(close, 5) > 0", false)]
    public void Validate_EnforcesContext(string context, string expression, bool expectedValid)
    {
        var result = _handler.Validate(new FilterValidateRequest { Expressions = [expression], Context = context });

        Assert.Equal(HttpStatusCode.OK, result.Status);
        var one = Assert.Single(result.Data!.Results);
        Assert.Equal(expectedValid, one.Valid);
        if (!expectedValid)
        {
            Assert.Contains("not available in chart", one.Error);
        }
    }

    [Fact]
    public void Validate_UnknownContext_IsBadRequest()
    {
        var result = _handler.Validate(new FilterValidateRequest { Expressions = ["close > 1"], Context = "nope" });
        Assert.Equal(HttpStatusCode.BadRequest, result.Status);
    }
}
