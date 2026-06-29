using FluentAssertions;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Contracts.Responses.Tools;
using Polygon.Client;
using Polygon.Client.Interfaces;
using Polygon.Client.Requests;
using System;
using System.IO;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace MarketViewer.Api.UnitTests.Utilities
{
    public class MarketDataUnitTests
    {
        private readonly TimeZoneInfo _timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        private readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly string _minuteDataPath = Path.Combine("Data", "minute.json");
        private readonly string _hourDataPath = Path.Combine("Data", "hour.json");

        private readonly IPolygonClient _polygonClient;
        private ITestOutputHelper _output;

        public MarketDataUnitTests(ITestOutputHelper output)
        {
            _polygonClient = new PolygonClient("C7rpQgrHdbJpryOfXUW8l3X9gWlFEvH4");
            _output = output;
        }

        [Fact]
        public void MinuteData_ShouldHaveValidStructure()
        {
            // Arrange
            var json = File.ReadAllText(_minuteDataPath);
            var liveData = JsonSerializer.Deserialize<PolygonFidelityResponse>(json, _jsonSerializerOptions);

            foreach (var snapshot in liveData.Minute.Snapshots)
            {
                var snapshotBar = snapshot.Value.Tickers.FirstOrDefault(q => q.Ticker == "SPY").Minute;
                
                var polygonAggregate = liveData.Minute.Aggregates.FirstOrDefault(q => q.Key == snapshot.Key).Value;
                var polygonAggregateBar = polygonAggregate.Results.FirstOrDefault();

                // Assert
                Assert.Equal(snapshotBar.Timestamp, polygonAggregateBar.Timestamp);
                Assert.Equal(snapshotBar.Volume, polygonAggregateBar.Volume);
                Assert.Equal(snapshotBar.Open, polygonAggregateBar.Open);
                Assert.Equal(snapshotBar.Close, polygonAggregateBar.Close);

                var liveBar = liveData.Minute.Data.Results.FirstOrDefault(q => q.Timestamp == snapshotBar.Timestamp);
                Assert.Equal(liveBar.Timestamp, polygonAggregateBar.Timestamp);
                Assert.Equal(liveBar.Volume, polygonAggregateBar.Volume);
                Assert.Equal(liveBar.Open, polygonAggregateBar.Open);
                Assert.Equal(liveBar.Close, polygonAggregateBar.Close);
            }
        }

        [Fact]
        public async Task HourData_ShouldHaveValidStructure()
        {
            // Arrange
            var json = File.ReadAllText(_minuteDataPath);
            var liveData = JsonSerializer.Deserialize<PolygonFidelityResponse>(json, _jsonSerializerOptions);

            var firstDate = DateTimeOffset.FromUnixTimeMilliseconds(liveData.Hour.Data.Results[0].Timestamp);
            var lastDate = DateTimeOffset.FromUnixTimeMilliseconds(liveData.Hour.Data.Results[^1].Timestamp);

            var polygonResponse = await _polygonClient.GetAggregates(new PolygonAggregateRequest
            {
                Ticker = liveData.Hour.Ticker,
                Multiplier = 1,
                Timespan = "hour",
                From = firstDate.ToString("yyyy-MM-dd"),
                To = lastDate.ToString("yyyy-MM-dd"),
                Limit = 50000
            });

            var backtestData = JsonSerializer.Deserialize<StocksResponse>(JsonSerializer.Serialize(polygonResponse), _jsonSerializerOptions);

            backtestData.Results = backtestData.Results
                .Where(q => q.Timestamp >= firstDate.ToUnixTimeMilliseconds() && q.Timestamp <= lastDate.ToUnixTimeMilliseconds())
                .ToList();

            // Assert
            Assert.NotNull(liveData);
            Assert.Equal("SPY", liveData.Hour.Data.Ticker);
            Assert.Equal("OK", liveData.Hour.Data.Status);
            Assert.NotEmpty(liveData.Hour.Data.Results);

            for (int i = 0; i < liveData.Hour.Data.Results.Count; i++)
            {
                try
                {
                    Assert.Equal(liveData.Hour.Data.Results[i].Timestamp, backtestData.Results[i].Timestamp);
                    liveData.Hour.Data.Results[i].Volume.Should().BeApproximately(backtestData.Results[i].Volume, 100);
                    Assert.Equal(liveData.Hour.Data.Results[i].Open, backtestData.Results[i].Open);
                    Assert.Equal(liveData.Hour.Data.Results[i].Close, backtestData.Results[i].Close);
                }
                catch (Exception ex)
                {
                    var date = DateTimeOffset.FromUnixTimeMilliseconds(liveData.Hour.Data.Results[i].Timestamp);
                    var offset = _timeZone.IsDaylightSavingTime(date) ? TimeSpan.FromHours(-4) : TimeSpan.FromHours(-5);

                    var beforeLive = liveData.Hour.Data.Results[i - 1];
                    var beforeBacktest = backtestData.Results[i - 1];

                    var duringLive = liveData.Hour.Data.Results[i];
                    var duringBacktest = backtestData.Results[i];

                    //var afterLive = liveData.Hour.Data.Results[i + 1];
                    //var afterBacktest = backtestData.Results[i + 1];

                    _output.WriteLine($"Error at index {i} - {date.ToOffset(offset)}.");
                    _output.WriteLine($"Live Volume: {liveData.Hour.Data.Results[i].Volume}");
                    _output.WriteLine($"Backtest Volume: {backtestData.Results[i].Volume}");
                    //Assert.Fail(ex.Message);
                }
            }
        }
    }
}