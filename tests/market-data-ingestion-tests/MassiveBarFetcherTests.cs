using System.Net;
using System.Text.Json;
using StockMountain.MarketData;
using StockMountain.MarketData.Ingestion.Massive;

namespace StockMountain.MarketData.Ingestion.Tests;

public class MassiveAggregateBarNormalizerTests
{
    [Fact]
    public void ToNormalizedBar_UsesUtcMidnightTimestampForDailyBars()
    {
        var aggregate = new MassiveAggregateBar
        {
            TimestampMilliseconds = 1_704_067_200_000,
            Open = 150m,
            High = 152m,
            Low = 149m,
            Close = 151m,
            Volume = 123_456_789,
            Vwap = 150.5m,
            TransactionCount = 500_000,
        };

        var bar = MassiveAggregateBarNormalizer.ToNormalizedBar(aggregate, new Timeframe(1, TimeframeUnit.Day));

        Assert.Equal(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), bar.TimestampUtc);
        Assert.Equal(151m, bar.Close);
        Assert.Equal(500_000, bar.TransactionCount);
    }

    [Fact]
    public void ToNormalizedBar_FloorsOneMinuteBarsToUtcMinute()
    {
        var aggregate = new MassiveAggregateBar
        {
            TimestampMilliseconds = DateTimeOffset.Parse("2026-01-15T14:30:45.123Z").ToUnixTimeMilliseconds(),
            Open = 150m,
            High = 152m,
            Low = 149m,
            Close = 151m,
            Volume = 1000,
            Vwap = 150.5m,
            TransactionCount = 50,
        };

        var bar = MassiveAggregateBarNormalizer.ToNormalizedBar(aggregate, new Timeframe(1, TimeframeUnit.Minute));

        Assert.Equal(new DateTimeOffset(2026, 1, 15, 14, 30, 0, TimeSpan.Zero), bar.TimestampUtc);
    }

    [Fact]
    public void ToNormalizedBar_StripsSubSecondPrecisionForFiveMinuteBars()
    {
        var aggregate = new MassiveAggregateBar
        {
            TimestampMilliseconds = DateTimeOffset.Parse("2026-01-15T14:30:45.123Z").ToUnixTimeMilliseconds(),
            Open = 150m,
            High = 152m,
            Low = 149m,
            Close = 151m,
            Volume = 1000,
            Vwap = 150.5m,
            TransactionCount = 50,
        };

        var bar = MassiveAggregateBarNormalizer.ToNormalizedBar(aggregate, new Timeframe(5, TimeframeUnit.Minute));

        Assert.Equal(new DateTimeOffset(2026, 1, 15, 14, 30, 45, TimeSpan.Zero), bar.TimestampUtc);
    }

    [Fact]
    public async Task DeserializeAggregateBar_AcceptsFloatingPointVolume()
    {
        const string json = """
            {
              "v": 80960300.0,
              "vw": 150.5,
              "o": 150.0,
              "c": 151.0,
              "h": 152.0,
              "l": 149.0,
              "t": 1704067200000,
              "n": 500000.0
            }
            """;

        var aggregate = await JsonSerializer.DeserializeAsync<MassiveAggregateBar>(
            new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)),
            MassiveJsonSerializerOptions.Create());

        Assert.NotNull(aggregate);
        Assert.Equal(80_960_300, aggregate.Volume);
        Assert.Equal(500_000, aggregate.TransactionCount);
    }
}

public class MassiveBarFetcherTests
{
    [Fact]
    public async Task FetchAsync_ParsesDailyFixtureAndPaginates()
    {
        var handler = new QueueResponseHandler([
            await ReadFixtureAsync("massive-daily-aggs-aapl.json"),
            """{"results":[],"status":"OK"}""",
        ]);

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.massive.com") };
        var fetcher = new MassiveBarFetcher(httpClient, new MassiveBarFetcherOptions { ApiKey = "test-key" });
        var series = new BarSeriesKey(
            Symbol.Create("AAPL"),
            new Timeframe(1, TimeframeUnit.Day),
            AdjustmentPolicy.SplitAdjusted);

        var bars = await fetcher.FetchAsync(
            series,
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 1, 2, 23, 59, 59, TimeSpan.Zero));

        Assert.Equal(2, bars.Count);
        Assert.Contains("/range/1/day/2024-01-01/2024-01-02", handler.RequestPaths[0]);
        Assert.Equal(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), bars[0].TimestampUtc);
        Assert.Equal(new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero), bars[1].TimestampUtc);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task FetchAsync_UsesMillisecondTimestampsForOneMinuteBars()
    {
        var handler = new QueueResponseHandler([
            await ReadFixtureAsync("massive-minute-aggs-aapl.json"),
            """{"results":[],"status":"OK"}""",
        ]);

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.massive.com") };
        var fetcher = new MassiveBarFetcher(httpClient, new MassiveBarFetcherOptions { ApiKey = "test-key" });
        var series = new BarSeriesKey(
            Symbol.Create("AAPL"),
            new Timeframe(1, TimeframeUnit.Minute),
            AdjustmentPolicy.SplitAdjusted);

        var from = new DateTimeOffset(2026, 1, 15, 14, 30, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 1, 15, 14, 31, 0, TimeSpan.Zero);
        var bars = await fetcher.FetchAsync(series, from, to);

        Assert.Equal(2, bars.Count);
        Assert.Contains($"/range/1/minute/{from.ToUnixTimeMilliseconds()}/{to.ToUnixTimeMilliseconds()}", handler.RequestPaths[0]);
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 14, 30, 0, TimeSpan.Zero), bars[0].TimestampUtc);
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 14, 31, 0, TimeSpan.Zero), bars[1].TimestampUtc);
    }

    private static async Task<string> ReadFixtureAsync(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        return await File.ReadAllTextAsync(path);
    }

    private sealed class QueueResponseHandler(IReadOnlyList<string> bodies) : HttpMessageHandler
    {
        private int _index;

        public int RequestCount { get; private set; }

        public List<string> RequestPaths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestPaths.Add(request.RequestUri?.PathAndQuery ?? string.Empty);
            var body = bodies[Math.Min(_index, bodies.Count - 1)];
            _index++;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body),
            });
        }
    }
}
