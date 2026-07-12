using MarketViewer.Filters.Interfaces;
using Polygon.Client.Models;

namespace MarketViewer.Filters.Expressions;

/// <summary>
/// Expression that accesses built-in price data (close, open, high, low, vwap, volume),
/// per-candle time of day (time), or ticker-level fundamentals (float).
/// </summary>
public class DataAccessExpression(string fieldName) : IExpression
{
    // Candle timestamps are UTC; time-of-day comparisons are always in market (Eastern) time.
    private static readonly TimeZoneInfo EasternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

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
            series.Add(CreateBarResult(bar));
        }

        return series;
    }

    /// <summary>
    /// Builds the series result for a single bar. Shared with incremental session evaluation.
    /// </summary>
    public IIndicatorResult CreateBarResult(Bar bar)
    {
        if (_fieldName == "time")
        {
            var eastern = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(bar.Timestamp), EasternTimeZone);
            return new TimeIndicatorResult
            {
                Timestamp = bar.Timestamp,
                Value = eastern.Hour * 60 + eastern.Minute
            };
        }

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

        return new SimpleIndicatorResult
        {
            Timestamp = bar.Timestamp,
            Value = value
        };
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
