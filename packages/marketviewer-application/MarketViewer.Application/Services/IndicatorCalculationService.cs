using System;
using System.Collections.Generic;
using System.Linq;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Models.Indicator;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Filters.Expressions;
using MarketViewer.Filters.Functions.Indicators;
using MarketViewer.Filters.Interfaces;
using MarketViewer.Studies;
using Microsoft.Extensions.Logging;

namespace MarketViewer.Application.Services;

public interface IIndicatorCalculationService
{
    IndicatorResponse? Compute(Indicator indicator, StocksResponse stockData, Timeframe timeframe);
}

public class IndicatorCalculationService(StudyFactory studyFactory, ILogger<IndicatorCalculationService> logger) : IIndicatorCalculationService
{
    private readonly SmaFunction _smaFunction = new();
    private readonly EmaFunction _emaFunction = new();
    private readonly MacdFunction _macdFunction = new();
    private readonly RsiFunction _rsiFunction = new();
    private readonly SupportResistanceFunction _supportResistanceFunction = new();

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

        if (TryComputeWithFilters(indicator, context, out var series))
        {
            if (series.Count == 0)
            {
                return null;
            }

            return new IndicatorResponse
            {
                Name = BuildIndicatorName(indicator),
                Results = ConvertSeries(series)
            };
        }

        return studyFactory.Compute(indicator, stockData);
    }

    private bool TryComputeWithFilters(Indicator indicator, ExpressionContext context, out List<IIndicatorResult> series)
    {
        series = [];

        try
        {
            series = indicator.Type switch
            {
                StudyType.sma => ExecuteFunction(_smaFunction, indicator.Parameters, context),
                StudyType.ema => ExecuteFunction(_emaFunction, indicator.Parameters, context),
                StudyType.macd => ExecuteFunction(_macdFunction, indicator.Parameters, context),
                StudyType.rsi => ExecuteFunction(_rsiFunction, indicator.Parameters, context),
                StudyType.vwap => EvaluatePriceLiteral("vwap", context),
                StudyType.sr or StudyType.support_resistance => ExecuteFunction(_supportResistanceFunction, indicator.Parameters, context),
                _ => null
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to compute indicator {@Indicator}", indicator);
            series = null;
        }

        return series is not null;
    }

    private static List<IIndicatorResult> ExecuteFunction(ISeriesFunction function, string[]? parameters, ExpressionContext context)
    {
        var args = ConvertParameters(parameters);
        var result = function.Execute(args, context);
        return result as List<IIndicatorResult> ?? [];
    }

    private static List<IIndicatorResult> EvaluatePriceLiteral(string fieldName, ExpressionContext context)
    {
        var expression = new DataAccessExpression(fieldName);
        var result = expression.Evaluate(context);
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
