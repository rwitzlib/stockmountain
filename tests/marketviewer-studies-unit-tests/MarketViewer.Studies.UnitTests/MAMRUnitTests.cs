using AutoFixture;
using Xunit;
using FluentAssertions;
using Polygon.Client.Models;
using MarketViewer.Contracts.Enums;
using System.Text.Json;
using FluentAssertions.Common;
using MarketViewer.Contracts.Responses.Market;
using Polygon.Client;
using MarketViewer.Contracts.Models.Indicator;

namespace MarketViewer.Studies.UnitTests;

public class MAMRUnitTests(StudyFixture studyFixture) : IClassFixture<StudyFixture>
{
    private readonly StudyFactory _classUnderTest = studyFixture.StudyFactory;
    private readonly IFixture _autoFixture = new Fixture();
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void MAMR_Works()
    {
        // Arrange
        var json = File.OpenText("./Data/other.json").ReadToEnd();
        var stocksResponse = JsonSerializer.Deserialize<StocksResponse>(json, _options);

        var lookback = 63;
        var cooldown = 21;
        List<IndicatorPoint> points = [];
        for (int i = 0; i < stocksResponse.Indicators.First().Results.Count; i++)
        {
            if (i <= lookback)
            {
                continue;
            }

            var range = stocksResponse.Indicators.First().Results.Skip(i - lookback).Take(lookback).ToList();

            var current = stocksResponse.Indicators.First().Results[i];

            if (current.Value > range.Max(q => q.Value))
            {
                points.Add(current);
                i += cooldown;
            }
        }

        var horizon = 21;
        List<float> profits = [];
        foreach (var point in points)
        {
            var index = stocksResponse.Results.FindIndex(q => q.Timestamp == point.Timestamp);

            var newIndex = index + horizon;
            if (newIndex >= stocksResponse.Results.Count)
            {
                newIndex = stocksResponse.Results.Count - 1;
            }

            var future = stocksResponse.Results[newIndex];

            var profit = ((future.Close - stocksResponse.Results[index].Close) / stocksResponse.Results[index].Close) * 100;

            profits.Add(profit);
        }

        var avgProfit = profits.Average();
        float winRate = (float)profits.Where(q => q > 0).Count() / profits.Count;

        // Assert
        avgProfit.Should().BeGreaterThan(0);
        winRate.Should().BeGreaterThan(.5f);
    }
}
