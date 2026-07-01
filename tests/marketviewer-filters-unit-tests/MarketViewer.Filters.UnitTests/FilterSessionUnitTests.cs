using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Enums;
using Polygon.Client.Models;
using Xunit;
using System.Text.Json;

namespace MarketViewer.Filters.UnitTests;

public class FilterSessionUnitTests
{
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly TimeSpan _offset = TimeZoneInfo.FindSystemTimeZoneById("America/New_York").GetUtcOffset(DateTime.UtcNow);

    [Fact]
    public void Session_Incremental_Yields_Same_Result_As_Full()
    {
        var engine = new IndicatorExpressionEngine();
        var session = engine.Compile("slope(close,5) > 0 AND rsi(14,70,30,ema) > 50");

        var timeframe = new Timeframe(1, Timespan.minute);
        var stockData = new StocksResponse { Results = [] };

        // Seed with initial data
        for (int i = 0; i < 100; i++)
        {
            stockData.Results.Add(new Bar { Timestamp = i, Close = 100 + i * 0.1f });
        }

        var fullFirst = session.Evaluate(stockData, timeframe);
        var incrFirst = session.EvaluateIncremental(stockData, timeframe);

        Assert.Equal(fullFirst, incrFirst);

        // Append bars, check on each step
        for (int i = 100; i < 150; i++)
        {
            stockData.Results.Add(new Bar { Timestamp = i, Close = 100 + i * 0.1f });

            var full = session.Evaluate(stockData, timeframe);
            var incr = session.EvaluateIncremental(stockData, timeframe);

            Assert.Equal(full, incr);
        }

        // Rewind test should reset
        stockData.Results.RemoveRange(110, stockData.Results.Count - 110);
        var fullAfterRewind = session.Evaluate(stockData, timeframe);
        var incrAfterRewind = session.EvaluateIncremental(stockData, timeframe);
        Assert.Equal(fullAfterRewind, incrAfterRewind);
    }

    
    // [Fact]
    public async Task Session_Incremential_Yields_Valid_Indicator_Result()
    {
        var json = await File.ReadAllTextAsync("TestData/tsla_1_minute_2025-09-27.json");
        var stocksResponse = JsonSerializer.Deserialize<StocksResponse>(json, _jsonOptions);
        ArgumentNullException.ThrowIfNull(stocksResponse);
        var nextBars = stocksResponse.Results.GetRange(200, stocksResponse.Results.Count - 200);
        stocksResponse.Results.RemoveRange(200, stocksResponse.Results.Count - 200);

        var engine = new IndicatorExpressionEngine();
        var session = engine.Compile("macd(12,26,9,ema) > 0");

        foreach (var bar in nextBars)
        {
            var result = session.EvaluateIncremental(stocksResponse, new Timeframe(1, Timespan.minute));
            stocksResponse.Results.Add(bar);

            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(bar.Timestamp).ToOffset(_offset);

            if (timestamp is { Day: 24, Hour: 9, Minute: 59 })
            {
                Assert.False(result);
            }

            if (timestamp is { Day: 24, Hour: 10, Minute: 9 })
            {
                Assert.True(result);
            }

            if (timestamp.Day == 26 && timestamp is { Hour: 6, Minute: 00 })
            {
                Assert.True(result);
            }

            if (timestamp is { Day: 26, Hour: 7, Minute: 4 })
            {
                Assert.False(result);
            }
        }
    }
}
