using MarketViewer.Filters.Expressions;
using Xunit;

namespace MarketViewer.Filters.UnitTests;

public class HeuristicsTests
{
    private readonly IndicatorExpressionEngine _engine = new();

    [Fact]
    public void CostOrdering_Adv_Sma_Macd()
    {
        var adv = ExpressionPlanner.Analyze(_engine.ParseExpression("adv() > 50 [5m, 1]"));
        var sma = ExpressionPlanner.Analyze(_engine.ParseExpression("sma(9) > 50 [5m, 1]"));
        var macd = ExpressionPlanner.Analyze(_engine.ParseExpression("macd(12,26,9,ema).value > 0 [5m, 1]"));

        Assert.True(adv.EstimatedCost <= sma.EstimatedCost);
        Assert.True(sma.EstimatedCost <= macd.EstimatedCost);
    }

    [Fact]
    public void Wrappers_DoNotZeroOutCosts()
    {
        var baseExpr = ExpressionPlanner.Analyze(_engine.ParseExpression("sma(3) > 100"));
        var withField = ExpressionPlanner.Analyze(_engine.ParseExpression("sma(3).value > 100"));
        var withTimeframe = ExpressionPlanner.Analyze(_engine.ParseExpression("sma(3) > 100 [5m, 2]"));

        Assert.True(withField.EstimatedCost >= baseExpr.EstimatedCost);
        Assert.True(withTimeframe.EstimatedCost >= baseExpr.EstimatedCost);
    }

    [Fact]
    public void LogicalExpectedCost_UsesShortCircuiting()
    {
        var cheap = ExpressionPlanner.Analyze(_engine.ParseExpression("adv() > 50"));
        var expensive = ExpressionPlanner.Analyze(_engine.ParseExpression("macd(12,26,9,ema).value > 0"));

        var andExpr = ExpressionPlanner.Analyze(_engine.ParseExpression("adv() > 50 AND macd(12,26,9,ema).value > 0"));
        var orExpr = ExpressionPlanner.Analyze(_engine.ParseExpression("adv() > 50 OR macd(12,26,9,ema).value > 0"));

        // Expected cost should be less than or equal to both-evaluated sum (plus tiny overhead)
        var sum = cheap.EstimatedCost + expensive.EstimatedCost + 0.2;
        Assert.True(andExpr.EstimatedCost <= sum);
        Assert.True(orExpr.EstimatedCost <= sum);
    }
}


