using AutoFixture;
using FluentAssertions;
using FluentAssertions.Common;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Models.Indicator;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Studies.Studies;
using Moq;
using Massive.Client.Models;
using System.Text.Json;
using Xunit;

namespace MarketViewer.Studies.UnitTests;

public class RVOLUnitTests(StudyFixture studyFixture) : IClassFixture<StudyFixture>
{
    private readonly StudyFactory _classUnderTest = studyFixture.StudyFactory;
    private readonly IFixture _autoFixture = new Fixture();
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void RVOL_Returns_Valid_Response()
    {
        // Arrange
        var json = File.OpenText("./Data/minute.json").ReadToEnd();
        var minuteStocksResponse = JsonSerializer.Deserialize<StocksResponse>(json, _options);

        var dayJson = File.OpenText("./Data/day.json").ReadToEnd();
        var dayStocksResponse = JsonSerializer.Deserialize<StocksResponse>(dayJson, _options);

        var indicator = new Indicator
        {
            Type = StudyType.rvol,
            Parameters = null
        };

        var dateTime = new DateTime(2025, 2, 26, 12, 0, 0);
        var offset = studyFixture.TimeZone.IsDaylightSavingTime(dateTime) ? TimeSpan.FromHours(-4) : TimeSpan.FromHours(-5);
        var timestamp = dateTime.ToDateTimeOffset(offset).ToUnixTimeMilliseconds();

        studyFixture.MarketCache.Setup(q => q.GetStocksResponse(It.IsAny<string>(), It.IsAny<Timeframe>(), It.IsAny<DateTimeOffset>())).Returns(dayStocksResponse);

        // Act
        var response = _classUnderTest.Compute(indicator, minuteStocksResponse);

        // Assert
        response.Results.Should().NotBeNull();

        var candle = response.Results.Single(q => q.Timestamp == timestamp);
        candle.Value.Should().BeApproximately(.517f, .01f);
    }

    [Fact]
    public void RVOL_With_Empty_Parameters_Returns_Null()
    {
        // Arrange
        var json = File.OpenText("./Data/minute.json").ReadToEnd();
        var minuteStocksResponse = JsonSerializer.Deserialize<StocksResponse>(json, _options);

        var indicator = new Indicator
        {
            Type = StudyType.rvol,
            Parameters = []
        };

        // Act
        var response = _classUnderTest.Compute(indicator, minuteStocksResponse);

        // Assert
        response.Should().BeNull();
    }

    [Fact]
    public void RVOL_With_No_Parameters_Returns_Null()
    {
        // Arrange
        var json = File.OpenText("./Data/minute.json").ReadToEnd();
        var minuteStocksResponse = JsonSerializer.Deserialize<StocksResponse>(json, _options);

        var indicator = new Indicator
        {
            Type = StudyType.rvol,
            Parameters = null
        };

        // Act
        var response = _classUnderTest.Compute(indicator, minuteStocksResponse);

        // Assert
        response.Should().BeNull();
    }
}
