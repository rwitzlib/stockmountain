using MarketViewer.Filters.Registry;
using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Functions.Comparison;

/// <summary>
/// True when series1 crosses from at-or-above to strictly below series2 on the latest bar
/// (or on any bar in the candle range). Either argument may be a number, which is treated as a
/// constant series so level crosses (`crosses_under(rsi(14,70,30,wilders), 70)`) work.
/// Spec: docs/filters/crosses_under.md.
/// </summary>
[FilterFunction("crosses_under", Kind = FunctionKind.Boolean,
    Signature = "crosses_under(series1, series2)", Snippet = "crosses_under(close, sma(20))",
    Description = "True when series1 crosses below series2 on the latest bar; either side may be a fixed level",
    Params = ["series1", "series2"], Cost = 3, Selectivity = 0.2, Contexts = FilterContext.Filters)]
public class CrossesUnderFunction : IBooleanFunction
{
    private const string Signature = "crosses_under(series1, series2)";

    public string Name => "crosses_under";

    public object Execute(object[] parameters, ExpressionContext context) =>
        CrossDetector.Detect(Name, Signature, parameters, context, over: false);
}
