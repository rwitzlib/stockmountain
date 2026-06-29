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

public class RSIUnitTests(StudyFixture studyFixture) : IClassFixture<StudyFixture>
{
    private readonly StudyFactory _classUnderTest = studyFixture.StudyFactory;
    private readonly IFixture _autoFixture = new Fixture();
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void RSI_With_No_Parameters_Returns_Null()
    {
        // Arrange 
        var indicator = new Indicator
        {
            Type = StudyType.rsi,
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
    public void RSI_With_Invalid_Type_Returns_Null()
    {
        // Arrange 
        var indicator = new Indicator
        {
            Type = StudyType.rsi,
            Parameters = ["14", "70", "30", "invalid"]
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
    public void RSI_With_Invalid_Weight_Returns_Null()
    {
        // Arrange 
        var indicator = new Indicator
        {
            Type = StudyType.rsi,
            Parameters = ["invalid", "70", "30", "sma"]
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

    [Theory]
    [InlineData("SMA", 22.38f)]
    [InlineData("EMA", 16.73f)]
    //[InlineData("Wilders", 64.82f)]
    public void RSI_Calculation_Is_Valid(string type, float expectedValue)
    {
        // Arrange
        var json = File.OpenText("./Data/minute.json").ReadToEnd();
        var stocksResponse = JsonSerializer.Deserialize<StocksResponse>(json, _options);

        var indicator = new Indicator
        {
            Type = StudyType.rsi,
            Parameters = ["14", "70", "30", type]
        };

        var dateTime = new DateTime(2025, 2, 26, 12, 0, 0);
        var offset = studyFixture.TimeZone.IsDaylightSavingTime(dateTime) ? TimeSpan.FromHours(-4) : TimeSpan.FromHours(-5);
        var timestamp = dateTime.ToDateTimeOffset(offset).ToUnixTimeMilliseconds();

        // Act
        var response = _classUnderTest.Compute(indicator, stocksResponse);

        // Assert
        response.Results.Should().NotBeNull();

        var candle = response.Results.Single(q => q.Timestamp == timestamp);
        candle.Value.Should().BeApproximately(expectedValue, .01f);
    }
}
