using MarketViewer.Filters.Registry;
using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Functions.Comparison;

/// <summary>
/// True when series1 crosses from at-or-below to strictly above series2 on the latest bar
/// (or on any bar in the candle range). Either argument may be a number, which is treated as a
/// constant series so level crosses (`crosses_over(rsi(14,70,30,wilders), 30)`) work.
/// Spec: docs/filters/crosses_over.md.
/// </summary>
[FilterFunction("crosses_over", Kind = FunctionKind.Boolean,
    Signature = "crosses_over(series1, series2)", Snippet = "crosses_over(close, sma(20))",
    Description = "True when series1 crosses above series2 on the latest bar; either side may be a fixed level",
    Params = ["series1", "series2"], Cost = 3, Selectivity = 0.2, Contexts = FilterContext.Filters)]
public class CrossesOverFunction : IBooleanFunction
{
    private const string Signature = "crosses_over(series1, series2)";

    public string Name => "crosses_over";

    public object Execute(object[] parameters, ExpressionContext context) =>
        CrossDetector.Detect(Name, Signature, parameters, context, over: true);
}
