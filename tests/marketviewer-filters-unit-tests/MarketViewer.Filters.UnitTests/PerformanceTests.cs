using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Contracts.Enums;
using Polygon.Client.Models;
using Xunit;
using System.Diagnostics;
using MarketViewer.Filters.Interfaces;
using MarketViewer.Filters.Functions.Indicators;
using MarketViewer.Contracts.Models;

namespace MarketViewer.Filters.UnitTests;

public class PerformanceTests
{
    private readonly IndicatorExpressionEngine _engine;

    public PerformanceTests()
    {
        _engine = new IndicatorExpressionEngine();
    }

    [Fact]
    public void TestMacdFieldAccess()
    {
        // Arrange
        var sp = new Stopwatch();
        sp.Start();
        var timeframe = new Timeframe(1, Timespan.minute);
        var stockData = new StocksResponse
        {
            Results = []
        };
        for (int i = 0; i < 1000; i++)
        {
            stockData.Results.Add(new Bar { Timestamp = i, Close = 100 + i * 0.1f });
        };

        // Act & Assert
        for (int i = 0; i < 390; i++)
        {
            for (int j = 0; j < 100; j++)
            {
                _ = _engine.EvaluateScript("macd(12,26,9,ema).value > 0", stockData, timeframe);
            }
        }

        sp.Stop();
        
        var elapsed = sp.ElapsedMilliseconds;
    }

    [Fact]
    public void Slope_Incremental_Is_Faster_Than_Full_Recompute()
    {
        var timeframe = new Timeframe(1, Timespan.minute);
        var stockData = new StocksResponse { Results = [] };
        for (int i = 0; i < 2000; i++)
        {
            stockData.Results.Add(new Bar { Timestamp = i, Close = 100 + i * 0.1f });
        }
        var context = new ExpressionContext { StockData = stockData, Timeframe = timeframe };

        var slope = new Functions.Transforms.SlopeFunction();
        int period = 14;

        // Baseline: full recompute on each new bar
        var swFull = Stopwatch.StartNew();
        for (int step = 0; step < 500; step++)
        {
            stockData.Results.Add(new Bar { Timestamp = 2000 + step, Close = 100 + (2000 + step) * 0.1f });
            var closeSeries = (List<IIndicatorResult>)new Expressions.DataAccessExpression("close").Evaluate(context);
            _ = (List<double>)slope.Execute([closeSeries, period], context);
        }
        swFull.Stop();

        // Reset to initial
        stockData.Results.RemoveRange(2000, 500);

        // Incremental: execute once then append
        var closeInitial = (List<IIndicatorResult>)new Expressions.DataAccessExpression("close").Evaluate(context);
        var prev = (List<double>)slope.Execute([closeInitial, period], context);

        var swIncr = Stopwatch.StartNew();
        for (int step = 0; step < 500; step++)
        {
            stockData.Results.Add(new Bar { Timestamp = 2000 + step, Close = 100 + (2000 + step) * 0.1f });
            var closeSeries = (List<IIndicatorResult>)new Expressions.DataAccessExpression("close").Evaluate(context);
            prev = (List<double>)((IIncrementalSeriesFunction)slope).Append([closeSeries, period], context, prev);
        }
        swIncr.Stop();

        Assert.True(swIncr.ElapsedMilliseconds < swFull.ElapsedMilliseconds);
    }

    [Fact]
    public void Rsi_Incremental_Is_Faster_Than_Full_Recompute()
    {
        var timeframe = new Timeframe(1, Timespan.minute);
        var stockData = new StocksResponse { Results = [] };
        for (int i = 0; i < 2000; i++)
        {
            stockData.Results.Add(new Bar { Timestamp = i, Close = 100 + (float)(Math.Sin(i / 25.0) * 2) + i * 0.02f });
        }
        var context = new ExpressionContext { StockData = stockData, Timeframe = timeframe };

        var rsi = new RsiFunction();
        int period = 14; double ob = 70, os = 30; string type = "wilders";

        int count = 5000;
        // Full recompute
        var swFull = Stopwatch.StartNew();
        for (int step = 0; step < count; step++)
        {
            stockData.Results.Add(new Bar { Timestamp = 2000 + step, Close = 100 + (float)(Math.Sin((2000 + step) / 25.0) * 2) + (2000 + step) * 0.02f });
            _ = (List<IIndicatorResult>)rsi.Execute([period, ob, os, type], context);
        }
        swFull.Stop();

        // Reset to initial
        stockData.Results.RemoveRange(2000, count);

        // Incremental: execute once then append
        var prev = (List<IIndicatorResult>)rsi.Execute([period, ob, os, type], context);
        var swIncr = Stopwatch.StartNew();
        for (int step = 0; step < count; step++)
        {
            stockData.Results.Add(new Bar { Timestamp = 2000 + step, Close = 100 + (float)(Math.Sin((2000 + step) / 25.0) * 2) + (2000 + step) * 0.02f });
            prev = (List<IIndicatorResult>)((IIncrementalSeriesFunction)rsi).Append([period, ob, os, type], context, prev);
        }
        swIncr.Stop();

        Assert.True(swIncr.ElapsedMilliseconds < swFull.ElapsedMilliseconds);
    }

    [Fact]
    public void Macd_Incremental_Is_Faster_Than_Full_Recompute()
    {
        var timeframe = new Timeframe(1, Timespan.minute);
        var stockData = new StocksResponse { Results = [] };
        for (int i = 0; i < 400; i++)
        {
            stockData.Results.Add(new Bar { Timestamp = i, Close = 100 + (float)(Math.Sin(i / 25.0) * 2) + i * 0.02f });
        }
        var context = new ExpressionContext { StockData = stockData, Timeframe = timeframe };

        var macd = new MacdFunction();
        int fast = 12; double slow = 26, signal = 9; string type = "ema";

        int count = 400;
        // Full recompute
        var swFull = Stopwatch.StartNew();
        for (int step = 0; step < count; step++)
        {
            stockData.Results.Add(new Bar { Timestamp = 2000 + step, Close = 100 + (float)(Math.Sin((2000 + step) / 25.0) * 2) + (2000 + step) * 0.02f });
            _ = (List<IIndicatorResult>)macd.Execute([fast, slow, signal, type], context);
        }
        swFull.Stop();

        // Reset to initial
        stockData.Results.RemoveRange(400, count);

        // Incremental: execute once then append
        var prev = (List<IIndicatorResult>)macd.Execute([fast, slow, signal, type], context);
        var swIncr = Stopwatch.StartNew();
        for (int step = 0; step < count; step++)
        {
            stockData.Results.Add(new Bar { Timestamp = 2000 + step, Close = 100 + (float)(Math.Sin((2000 + step) / 25.0) * 2) + (2000 + step) * 0.02f });
            prev = (List<IIndicatorResult>)((IIncrementalSeriesFunction)macd).Append([fast, slow, signal, type], context, prev);
        }
        swIncr.Stop();

        Assert.True(swIncr.ElapsedMilliseconds < swFull.ElapsedMilliseconds);
    }

    [Fact]
    public void FilterSession_Incremental_Is_Faster_Than_Full_Recompute()
    {
        var timeframe = new Timeframe(1, Timespan.minute);
        var stockData = new StocksResponse { Results = [] };
        int count = 400;
        for (int i = 0; i < count; i++)
        {
            stockData.Results.Add(new Bar { Timestamp = i, Close = 100 + (float)(Math.Sin(i / 25.0) * 2) + i * 0.02f });
        }
        var context = new ExpressionContext { StockData = stockData, Timeframe = timeframe };

        var engine = new IndicatorExpressionEngine();
        var expression = engine.ParseExpression("macd(12,26,9,ema).value > 0");

        var session = engine.Compile("macd(12,26,9,ema).value > 0");

        // Full recompute
        var swFull = Stopwatch.StartNew();
        for (int step = 0; step < count; step++)
        {
            _ = engine.EvaluateExpression(expression, stockData, timeframe);
            stockData.Results.Add(new Bar { Timestamp = 2000 + step, Close = 100 + (float)(Math.Sin((2000 + step) / 25.0) * 2) + (2000 + step) * 0.02f });
        }
        swFull.Stop();

        // Reset to initial
        stockData.Results.RemoveRange(count, count);

        // Incremental: execute once then append
        var swIncr = Stopwatch.StartNew();
        for (int step = 0; step < count; step++)
        {
            _ = session.EvaluateIncremental(stockData, timeframe);
            stockData.Results.Add(new Bar { Timestamp = 2000 + step, Close = 100 + (float)(Math.Sin((2000 + step) / 25.0) * 2) + (2000 + step) * 0.02f });
        }
        swIncr.Stop();

        Assert.True(swIncr.ElapsedMilliseconds < swFull.ElapsedMilliseconds);
    }
}
