using MarketViewer.Filters.Interfaces;
using Massive.Client.Models;

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

        if (_fieldName == "time")
        {
            return EvaluateTime(context);
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
    /// "time" is the evaluation clock — the scan time for live scans, the simulated
    /// minute for backtests — not a per-bar attribute. A thin ticker whose last bar
    /// is stale must not keep satisfying a gate like "time &lt; 10:00" after the
    /// cutoff has passed. Falls back to the last bar's timestamp when no evaluation
    /// time was provided.
    /// </summary>
    private static List<IIndicatorResult> EvaluateTime(ExpressionContext context)
    {
        var timestamp = context.EvaluationTime?.ToUnixTimeMilliseconds()
            ?? context.StockData.Results?.LastOrDefault()?.Timestamp;

        return timestamp is long value ? [CreateTimeResult(value)] : [];
    }

    private static TimeIndicatorResult CreateTimeResult(long timestamp)
    {
        var eastern = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(timestamp), EasternTimeZone);
        return new TimeIndicatorResult
        {
            Timestamp = timestamp,
            Value = eastern.Hour * 60 + eastern.Minute
        };
    }

    /// <summary>
    /// Builds the series result for a single bar. Shared with incremental session evaluation.
    /// </summary>
    public IIndicatorResult CreateBarResult(Bar bar)
    {
        if (_fieldName == "time")
        {
            return CreateTimeResult(bar.Timestamp);
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
