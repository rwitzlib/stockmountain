using AutoFixture;
using FluentAssertions;
using FluentAssertions.Common;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models.Indicator;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Studies.Studies;
using Polygon.Client.Models;
using System.Text.Json;
using Xunit;

namespace MarketViewer.Studies.UnitTests;

public class VWAPUnitTests(StudyFixture studyFixture) : IClassFixture<StudyFixture>
{
    private readonly StudyFactory _classUnderTest = studyFixture.StudyFactory;
    private readonly IFixture _autoFixture = new Fixture();
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void VWAP_Returns_Valid_Response()
    {
        // Arrange
        var indicator = new Indicator
        {
            Type = StudyType.vwap,
            Parameters = null
        };
        var stocksResponse = new StocksResponse
        {
            Results = _autoFixture.CreateMany<Bar>(100).ToList()
        };

        // Act
        var response = _classUnderTest.Compute(indicator, stocksResponse);

        // Assert
        response.Results.Should().NotBeNull();
    }

    [Fact]
    public void VWAP_Intraday_Returns_Valid_Response()
    {
        // Arrange 
        var json = File.OpenText("./Data/minute.json").ReadToEnd();
        var stocksResponse = JsonSerializer.Deserialize<StocksResponse>(json, _options);

        var dateTime = new DateTime(2025, 2, 26, 12, 0, 0);
        var offset = studyFixture.TimeZone.IsDaylightSavingTime(dateTime) ? TimeSpan.FromHours(-4) : TimeSpan.FromHours(-5);
        var timestamp = dateTime.ToDateTimeOffset(offset).ToUnixTimeMilliseconds();

        stocksResponse.Results = stocksResponse.Results.Where(q => DateTimeOffset.FromUnixTimeMilliseconds(q.Timestamp).ToOffset(offset).Day == 26).ToList();

        // Act
        var response = _classUnderTest.Compute(new Indicator
        {
            Type = StudyType.vwap,
            Parameters = null
        }, stocksResponse);

        // Assert
        response.Results.Should().NotBeNull();
        response.Results.Single(q => q.Timestamp == timestamp).Value.Should().BeApproximately(301.99f, .25f);
    }

    [Fact]
    public void VWAP_With_No_Candles_Returns_Null()
    {
        // Arrange
        var indicator = new Indicator
        {
            Type = StudyType.vwap,
            Parameters = null
        };
        var stocksResponse = new StocksResponse
        {
            Results = new List<Bar>()
        };

        // Act
        var response = _classUnderTest.Compute(indicator, stocksResponse);

        // Assert
        response.Should().BeNull();
    }
}
