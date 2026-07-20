using AutoFixture;
using Moq.AutoMock;
using Xunit;
using FluentAssertions;
using Massive.Client.Models;
using System.Text.Json;
using MarketViewer.Studies.Studies;
using MarketViewer.Contracts.Enums;
using FluentAssertions.Common;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Contracts.Models.Indicator;

namespace MarketViewer.Studies.UnitTests;

public class SMAUnitTests(StudyFixture studyFixture) : IClassFixture<StudyFixture>
{
    private readonly StudyFactory _classUnderTest = studyFixture.StudyFactory;
    private readonly IFixture _autoFixture = new Fixture();
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void SMA_With_No_Parameters_Returns_Null()
    {
        // Arrange 
        var indicator = new Indicator
        {
            Type = StudyType.sma,
            Parameters = null
        };
        var stocksResponse = new StocksResponse
        {
            Results = _autoFixture.CreateMany<Bar>(100).ToList()
        };

        // Act
        var response = _classUnderTest.Compute(indicator, stocksResponse);

        // Assert
        response.Should().BeNull();
    }
    
    [Fact]
    public void SMA_With_Invalid_Parameters_Returns_Null()
    {
        // Arrange 
        var indicator = new Indicator
        {
            Type = StudyType.sma,
            Parameters = ["asdf"]
        };
        var stocksResponse = new StocksResponse
        {
            Results = _autoFixture.CreateMany<Bar>(100).ToList()
        };

        // Act
        var response = _classUnderTest.Compute(indicator, stocksResponse);

        // Assert
        response.Should().BeNull();
    }

    [Fact]
    public void SMA_With_Too_Many_Parameters_Returns_Null()
    {
        // Arrange 
        var indicator = new Indicator
        {
            Type = StudyType.sma,
            Parameters = ["9", "9"]
        };
        var stocksResponse = new StocksResponse
        {
            Results = _autoFixture.CreateMany<Bar>(100).ToList()
        };

        // Act
        var response = _classUnderTest.Compute(indicator, stocksResponse);

        // Assert
        response.Should().BeNull();
    }

    [Fact]
    public void SMA_With_Too_High_Or_Low_Weight_Returns_Null()
    {
        // Arrange
        var indicator = new Indicator
        {
            Type = StudyType.sma,
            Parameters = ["1", "1000"]
        };
        var stocksResponse = new StocksResponse
        {
            Results = _autoFixture.CreateMany<Bar>(100).ToList()
        };

        // Act
        var response = _classUnderTest.Compute(indicator, stocksResponse);

        // Assert
        response.Should().BeNull();
    }

    [Fact]
    public void SMA_With_No_Candles_Returns_Null()
    {
        // Arrange
        var indicator = new Indicator
        {
            Type = StudyType.sma,
            Parameters = ["9"]
        };
        var stocksResponse = new StocksResponse
        {
            Results = _autoFixture.CreateMany<Bar>(8).ToList()
        };

        // Act
        var response = _classUnderTest.Compute(indicator, stocksResponse);

        // Assert
        response.Should().BeNull();
    }

    [Fact]
    public void SMA_Returns_Valid_Response()
    {
        // Arrange
        var json = File.OpenText("./Data/minute.json").ReadToEnd();
        var stocksResponse = JsonSerializer.Deserialize<StocksResponse>(json, _options);

        var indicator = new Indicator
        {
            Type = StudyType.sma,
            Parameters = ["9"]
        };

        var dateTime = new DateTime(2025, 2, 26, 12, 0, 0);
        var offset = studyFixture.TimeZone.IsDaylightSavingTime(dateTime) ? TimeSpan.FromHours(-4) : TimeSpan.FromHours(-5);
        var timestamp = dateTime.ToDateTimeOffset(offset).ToUnixTimeMilliseconds();

        // Act
        var response = _classUnderTest.Compute(indicator, stocksResponse);

        // Assert
        response.Results.Should().NotBeNull();
        response.Results.Single(q => q.Timestamp == timestamp).Value.Should().BeApproximately(299.41f, .01f);
    }
}
