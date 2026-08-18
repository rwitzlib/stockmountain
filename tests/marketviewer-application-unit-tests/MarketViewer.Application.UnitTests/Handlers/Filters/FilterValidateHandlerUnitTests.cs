using MarketViewer.Application.Handlers.Market.Filters;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Requests.Market.Filters;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;
using System.Net;
using Xunit;

namespace MarketViewer.Application.UnitTests.Handlers.Filters;

public class FilterValidateHandlerUnitTests
{
    private readonly FilterValidateHandler _handler = new(NullLogger<FilterValidateHandler>.Instance);

    private Contracts.Responses.Market.Filters.FilterValidationResult ValidateOne(string expression)
    {
        var result = _handler.Validate(new FilterValidateRequest { Expressions = [expression] });
        Assert.Equal(HttpStatusCode.OK, result.Status);
        return Assert.Single(result.Data!.Results);
    }

    [Theory]
    [InlineData("close > vwap()")]
    [InlineData("close > vwap(day) [5m]")]
    [InlineData("rsi(14) < 30 [1m]")]
    [InlineData("macd(12,26,9,ema).histogram > 0 [5m]")]
    [InlineData("crosses_over(close, sma(20))")]
    [InlineData("adv() > 2000000 [1d]")]
    [InlineData("volume > 50000 [1m,5]")]
    [InlineData("float < 20000000")]
    [InlineData("close > sma(50) AND (rsi(14) < 30 OR rsi(14) > 70) [1m]")]
    [InlineData("NOT (close > sma(50) OR close > sma(200)) [1d]")]
    public void Validate_KnownGoodExpressions_AreValid(string expression)
    {
        var result = ValidateOne(expression);

        Assert.True(result.Valid, result.Error);
        Assert.NotNull(result.Ast);
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
    public void Validate_BadExpressions_ReturnError(string expression)
    {
        var result = ValidateOne(expression);

        Assert.False(result.Valid);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Null(result.Ast);
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
    public void Validate_CrossesOver_DescribesAsCrossing()
    {
        var result = ValidateOne("crosses_over(close, sma(20))");

        Assert.Equal("close crosses above sma(20)", result.Description);
    }

    [Fact]
    public void Validate_GroupedLogic_KeepsParenthesesInDescriptionAndNestsAst()
    {
        var result = ValidateOne("close > sma(20) AND (rsi(14) < 30 OR rsi(14) > 70) [5m]");

        Assert.True(result.Valid, result.Error);
        Assert.Equal("close is above sma(20) and (rsi(14) is below 30 or rsi(14) is above 70) on the 5m chart", result.Description);

        var and = result.Ast!.Inner!;
        Assert.Equal("AND", and.Op);
        Assert.Equal("OR", and.Right!.Op);
    }

    [Fact]
    public void Validate_SameOperatorChain_HasNoParentheses()
    {
        var result = ValidateOne("close > 1 AND close > 2 AND close > 3");

        Assert.Equal("close is above 1 and close is above 2 and close is above 3", result.Description);
    }

    [Fact]
    public void Validate_Not_MapsToUnaryNodeAndParenthesisesLogicalOperand()
    {
        var single = ValidateOne("NOT close > sma(20)");
        Assert.True(single.Valid, single.Error);
        Assert.Equal("unary", single.Ast!.Kind);
        Assert.Equal("NOT", single.Ast.Op);
        Assert.Equal("binary", single.Ast.Inner!.Kind);
        Assert.Equal("not close is above sma(20)", single.Description);

        var grouped = ValidateOne("NOT (close > 1 OR close > 2)");
        Assert.True(grouped.Valid, grouped.Error);
        Assert.Equal("not (close is above 1 or close is above 2)", grouped.Description);
    }

    [Fact]
    public void Validate_UnbalancedParenthesis_IsInvalid()
    {
        var result = ValidateOne("close > 1 AND (close > 2 OR close > 3");

        Assert.False(result.Valid);
        Assert.Contains("parenthesis", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Ast_ExposesBinaryStructureForChips()
    {
        var result = ValidateOne("rsi(14) < 30 [1m]");

        var range = result.Ast!;
        Assert.Equal("range", range.Kind);
        Assert.Equal(1, range.Timeframe!.Multiplier);

        var binary = range.Inner!;
        Assert.Equal("binary", binary.Kind);
        Assert.Equal("<", binary.Op);
        Assert.Equal("function", binary.Left!.Kind);
        Assert.Equal("rsi", binary.Left.Name);
        Assert.Equal("literal", binary.Right!.Kind);
        Assert.Equal("30", binary.Right.Value);
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
    public void Functions_IsExactlyTheRegistry()
    {
        var functions = _handler.Functions().Data!.Functions;

        Assert.Equal(
            MarketViewer.Filters.Registry.FunctionRegistry.All.Select(d => d.Name).OrderBy(n => n),
            functions.Select(f => f.Name).OrderBy(n => n));
        Assert.All(functions, f =>
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
    public void Functions_ChartContext_OnlyChartable()
    {
        var chart = _handler.Functions("chart").Data!.Functions;
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
