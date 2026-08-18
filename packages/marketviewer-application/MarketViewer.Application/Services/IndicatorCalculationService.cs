using System;
using System.Collections.Generic;
using System.Linq;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Models.Indicator;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Filters.Expressions;
using MarketViewer.Filters.Registry;
using MarketViewer.Filters.Interfaces;
using Microsoft.Extensions.Logging;

namespace MarketViewer.Application.Services;

public interface IIndicatorCalculationService
{
    IndicatorResponse? Compute(Indicator indicator, StocksResponse stockData, Timeframe timeframe);
}

public class IndicatorCalculationService(ILogger<IndicatorCalculationService> logger) : IIndicatorCalculationService
{
    // One instance per chartable function, keyed by every accepted name/alias. Functions are stateless.
    private static readonly Dictionary<string, ISeriesFunction> ChartFunctions = BuildChartFunctions();

    private static Dictionary<string, ISeriesFunction> BuildChartFunctions()
    {
        var map = new Dictionary<string, ISeriesFunction>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in FunctionRegistry.Functions.Where(d => d.SupportsContext(FilterContext.Chart) && d.IsSeriesFunction))
        {
            var instance = (ISeriesFunction)d.CreateFunction();
            foreach (var name in d.AllNames) map[name] = instance;
        }
        return map;
    }

    /// <summary>True when <paramref name="name"/> is a registered function that may be plotted via /stocks.</summary>
    public static bool IsChartable(string? name) => name is not null && ChartFunctions.ContainsKey(name);

    public IndicatorResponse? Compute(Indicator indicator, StocksResponse stockData, Timeframe timeframe)
    {
        ArgumentNullException.ThrowIfNull(indicator);
        ArgumentNullException.ThrowIfNull(stockData);
        ArgumentNullException.ThrowIfNull(timeframe);

        if (stockData.Results is null || stockData.Results.Count == 0)
        {
            return null;
        }

        var context = new ExpressionContext
        {
            StockData = stockData,
            Timeframe = timeframe
        };

        var series = ComputeSeries(indicator, context);
        if (series is null || series.Count == 0)
        {
            return null;
        }

        return new IndicatorResponse
        {
            Name = BuildIndicatorName(indicator),
            Results = ConvertSeries(series)
        };
    }

    /// <summary>
    /// Computes the indicator series via the MarketViewer.Filters functions (the same code paths
    /// used by /scan, the backtester and the live scanner). The function is resolved by name from
    /// the FunctionRegistry (Chart context only). Returns null when the name is unknown/not
    /// chartable or the function throws; the caller omits the indicator from the response.
    /// </summary>
    private List<IIndicatorResult>? ComputeSeries(Indicator indicator, ExpressionContext context)
    {
        if (indicator.Type is null || !ChartFunctions.TryGetValue(indicator.Type, out var function))
        {
            logger.LogWarning("Indicator {Type} is not a chartable filter function; skipping", indicator.Type);
            return null;
        }

        try
        {
            return ExecuteFunction(function, indicator.Parameters, context);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to compute indicator {@Indicator}", indicator);
            return null;
        }
    }

    private static List<IIndicatorResult> ExecuteFunction(ISeriesFunction function, string[]? parameters, ExpressionContext context)
    {
        var args = ConvertParameters(parameters);
        var result = function.Execute(args, context);
        return result as List<IIndicatorResult> ?? [];
    }

    private static object[] ConvertParameters(string[]? parameters)
    {
        if (parameters is null || parameters.Length == 0)
        {
            return Array.Empty<object>();
        }

        var converted = new object[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            converted[i] = parameters[i];
        }

        return converted;
    }

    private static List<IndicatorPoint> ConvertSeries(List<IIndicatorResult> series)
    {
        var result = new List<IndicatorPoint>(series.Count);

        foreach (var point in series)
        {
            var converted = ConvertPoint(point);
            if (converted is not null)
            {
                result.Add(converted);
            }
        }

        return result;
    }

    private static IndicatorPoint? ConvertPoint(IIndicatorResult point)
    {
        return point switch
        {
            MacdResult macd => new MacdPoint
            {
                Timestamp = macd.Timestamp,
                Value = ToFloat(macd.Value),
                Signal = ToFloat(macd.Signal),
                Histogram = ToFloat(macd.Histogram)
            },
            RsiResult rsi => new RsiPoint
            {
                Timestamp = rsi.Timestamp,
                Value = ToFloat(rsi.Value),
                Upper = ToFloat(rsi.Overbought),
                Lower = ToFloat(rsi.Oversold)
            },
            SupportResistanceResult zone => new SupportResistancePoint
            {
                Timestamp = zone.Timestamp,
                Value = ToFloat(zone.Value),
                Support = ToFloat(zone.Support),
                Resistance = ToFloat(zone.Resistance),
                SupportStrength = ToFloat(zone.SupportStrength),
                ResistanceStrength = ToFloat(zone.ResistanceStrength),
                SupportZoneWidth = ToFloat(zone.SupportZoneWidth),
                ResistanceZoneWidth = ToFloat(zone.ResistanceZoneWidth),
                SupportDistance = ToFloat(zone.SupportDistance),
                ResistanceDistance = ToFloat(zone.ResistanceDistance),
                SupportDistancePercent = ToFloat(zone.SupportDistancePercent),
                ResistanceDistancePercent = ToFloat(zone.ResistanceDistancePercent),
                SupportTouches = ToFloat(zone.SupportTouches),
                ResistanceTouches = ToFloat(zone.ResistanceTouches),
                SupportUpper = ToFloat(zone.SupportUpper),
                SupportLower = ToFloat(zone.SupportLower),
                ResistanceUpper = ToFloat(zone.ResistanceUpper),
                ResistanceLower = ToFloat(zone.ResistanceLower),
                NearSupport = ToFloat(zone.NearSupport),
                NearResistance = ToFloat(zone.NearResistance)
            },
            SimpleIndicatorResult simple => new IndicatorPoint
            {
                Timestamp = simple.Timestamp,
                Value = ToFloat(simple.Value)
            },
            _ => new IndicatorPoint
            {
                Timestamp = point.Timestamp,
                Value = ToFloat(point.GetFieldValue())
            }
        };
    }

    private static float ToFloat(double value)
    {
        return double.IsNaN(value) ? float.NaN : (float)value;
    }

    private static string BuildIndicatorName(Indicator indicatorParameters)
    {
        var name = $"{indicatorParameters.Type}";

        if (indicatorParameters.Parameters is not null && indicatorParameters.Parameters.Any())
        {
            name += $"({string.Join(',', indicatorParameters.Parameters)})";
        }

        if (!string.IsNullOrEmpty(indicatorParameters.Selector))
        {
            name += $".{indicatorParameters.Selector}";
        }

        return name;
    }
}
