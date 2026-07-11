using MarketViewer.Filters.Interfaces;

namespace MarketViewer.Filters.Expressions;

/// <summary>
/// Expression that accesses built-in price data (close, open, high, low, vwap, volume)
/// or ticker-level fundamentals (float).
/// </summary>
public class DataAccessExpression(string fieldName) : IExpression
{
    private readonly string _fieldName = fieldName.ToLowerInvariant();

    public string GetFieldName() => _fieldName;

    public bool IsScalar => _fieldName is "float";

    public object Evaluate(ExpressionContext context)
    {
        if (IsScalar)
        {
            return EvaluateScalar(context);
        }

        var data = context.StockData.Results;

        var series = new List<IIndicatorResult>();

        foreach (var bar in data)
        {
            var value = _fieldName switch
            {
                "close" => bar.Close,
                "open" => bar.Open,
                "high" => bar.High,
                "low" => bar.Low,
                "vwap" => bar.Vwap,
                "volume" => bar.Volume,
                _ => throw new ArgumentException($"Unknown data field: {_fieldName}")
            };

            series.Add(new SimpleIndicatorResult
            {
                Timestamp = bar.Timestamp,
                Value = value
            });
        }

        return series;
    }

    private object EvaluateScalar(ExpressionContext context)
    {
        return _fieldName switch
        {
            // Missing float should fail comparisons (NaN > x / NaN < x are false).
            "float" => context.StockData.TickerInfo?.TickerDetails?.Float is long floatValue
                ? (double)floatValue
                : double.NaN,
            _ => throw new ArgumentException($"Unknown scalar data field: {_fieldName}")
        };
    }
}
