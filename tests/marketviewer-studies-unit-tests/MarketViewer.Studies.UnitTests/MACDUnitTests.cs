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

public class MACDUnitTests(StudyFixture studyFixture) : IClassFixture<StudyFixture>
{
    private readonly StudyFactory _classUnderTest = studyFixture.StudyFactory;
    private readonly IFixture _autoFixture = new Fixture();
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void MACD_With_No_Parameters_Returns_Null()
    {
        // Arrange 
        var indicator = new Indicator
        {
            Type = StudyType.macd,
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
    public void MACD_With_Invalid_Type_Returns_Null()
    {
        // Arrange 
        var indicator = new Indicator
        {
            Type = StudyType.macd,
            Parameters = ["12", "26", "9", "invalid"]
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
    public void MACD_With_Invalid_Fast_Weight_Returns_Null()
    {
        // Arrange 
        var indicator = new Indicator
        {
            Type = StudyType.macd,
            Parameters = ["invalid", "26", "9", "sma"]
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
    public void MACD_With_Invalid_Slow_Weight_Returns_Null()
    {
        // Arrange 
        var indicator = new Indicator
        {
            Type = StudyType.macd,
            Parameters = ["12", "invalid", "9", "sma"]
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
    public void MACD_With_Invalid_Signal_Weight_Returns_Null()
    {
        // Arrange 
        var indicator = new Indicator
        {
            Type = StudyType.macd,
            Parameters = ["12", "26", "invalid", "sma"]
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
    public void MACD_With_No_Candles_Returns_Null()
    {
        // Arrange
        var indicator = new Indicator
        {
            Type = StudyType.macd,
            Parameters = ["12", "26", "9", "sma"]
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

    [Theory]
    [InlineData("12", "26", "9", "EMA", -.334)]
    [InlineData("12", "26", "9", "SMA", -.53)]
    //[InlineData("12", "26", "9", "Wilders", -.133)]
    //[InlineData("12", "26", "9", "Wilders", -.59)]
    //[InlineData("12", "26", "9", "Wilders", -.70)]
    public void MACD_Returns_Correct_Value(string fast, string slow, string signal, string type, float expectedValue)
    {
        // Arrange
        var json = File.OpenText("./Data/minute.json").ReadToEnd();
        var stocksResponse = JsonSerializer.Deserialize<StocksResponse>(json, _options);

        var indicator = new Indicator
        {
            Type = StudyType.macd,
            Parameters = [fast, slow, signal, type]
        };

        var dateTime = new DateTime(2025, 2, 26, 12, 0, 0);
        var offset = studyFixture.TimeZone.IsDaylightSavingTime(dateTime) ? TimeSpan.FromHours(-4) : TimeSpan.FromHours(-5);
        var timestamp = dateTime.ToDateTimeOffset(offset).ToUnixTimeMilliseconds();

        // Act
        var response = _classUnderTest.Compute(indicator, stocksResponse);

        // Assert
        response.Results.Should().NotBeNull();

        var candle = response.Results.Single(q => q.Timestamp == timestamp) as MacdPoint;
        candle.Value.Should().BeApproximately(expectedValue, .01f);
    }
}
