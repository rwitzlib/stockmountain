using MarketViewer.Application.Services;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Models.Indicator;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Filters.Registry;
using Massive.Client.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace MarketViewer.Application.UnitTests.Services;

/// <summary>
/// /stocks indicators resolve by name through the FunctionRegistry (Chart context) — plan 15 phase 4.
/// </summary>
public class IndicatorCalculationServiceUnitTests
{
    private readonly IndicatorCalculationService _service = new(NullLogger<IndicatorCalculationService>.Instance);

    private static StocksResponse Bars(int count) => new()
    {
        Ticker = "TEST",
        Status = "OK",
        Results = Enumerable.Range(0, count).Select(i => new Bar
        {
            Timestamp = 1_700_000_000_000 + i * 60_000L,
            Open = 100 + i, High = 101 + i, Low = 99 + i, Close = 100 + i, Volume = 1000, Vwap = 100 + i,
        }).ToList(),
    };

    private static readonly Timeframe Minute = new(1, Timespan.minute);

    [Theory]
    [InlineData("sma", "5")]
    [InlineData("ema", "5")]
    [InlineData("rsi", "5,70,30,wilders")]
    [InlineData("macd", "3,5,2,ema")]
    [InlineData("vwap", "")]
    [InlineData("sr", "")]
    [InlineData("support_resistance", "")]
    public void Compute_ChartableFunction_ReturnsSeries(string type, string parameters)
    {
        var indicator = new Indicator
        {
            Type = type,
            Parameters = parameters.Length == 0 ? Array.Empty<string>() : parameters.Split(','),
        };

        var result = _service.Compute(indicator, Bars(60), Minute);

        Assert.NotNull(result);
        Assert.NotEmpty(result!.Results);
        Assert.StartsWith(type, result.Name);
    }

    [Theory]
    [InlineData("float")]         // keyword, not a function
    [InlineData("crosses_over")]  // boolean, not chartable
    [InlineData("slope")]         // transform, Contexts = Filters
    [InlineData("adv")]           // Contexts = Filters
    [InlineData("nope")]          // unknown
    public void Compute_NonChartable_ReturnsNull(string type)
    {
        var result = _service.Compute(new Indicator { Type = type, Parameters = ["5"] }, Bars(60), Minute);
        Assert.Null(result);
    }

    [Fact]
    public void IsChartable_MatchesRegistryChartContext()
    {
        foreach (var d in FunctionRegistry.All)
        {
            var expected = !d.IsKeyword && d.SupportsContext(FilterContext.Chart);
            Assert.Equal(expected, IndicatorCalculationService.IsChartable(d.Name));
        }
    }
}
