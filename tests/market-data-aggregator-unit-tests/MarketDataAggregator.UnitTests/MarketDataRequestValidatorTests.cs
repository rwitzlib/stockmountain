using FluentAssertions;
using MarketDataAggregator;
using MarketDataAggregator.Validation;
using MarketViewer.Contracts.Enums;
using Xunit;

namespace MarketDataAggregator.UnitTests;

public class MarketDataAggregatorRequestValidatorTests
{
    private readonly MarketDataAggregatorRequestValidator _validator = new();

    [Fact]
    public void Valid_Manual_Request_Should_Pass()
    {
        var request = new MarketDataAggregatorRequest
        {
            Date = DateTimeOffset.Parse("2024-06-07"),
            Multiplier = 1,
            Timespan = Timespan.minute
        };

        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void Manual_Request_With_Invalid_Multiplier_Should_Fail(int multiplier)
    {
        var request = new MarketDataAggregatorRequest
        {
            Date = DateTimeOffset.Parse("2024-06-07"),
            Multiplier = multiplier,
            Timespan = Timespan.minute
        };

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage.Contains("Multiplier"));
    }

    [Fact]
    public void Weekend_Request_Should_Fail()
    {
        var request = new MarketDataAggregatorRequest
        {
            Date = DateTimeOffset.Parse("2024-06-08"),
            Multiplier = 1,
            Timespan = Timespan.minute
        };

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage.Contains("Saturday"));
    }
}

public class MarketDataOrchestratorRequestValidatorTests
{
    private readonly MarketDataOrchestratorRequestValidator _validator = new();

    [Fact]
    public void Valid_Request_Should_Pass()
    {
        var request = new MarketDataOrchestratorRequest
        {
            Start = DateTimeOffset.Parse("2024-06-03"),
            End = DateTimeOffset.Parse("2024-06-07"),
            Timespans = [Timespan.minute, Timespan.hour],
            Multiplier = 1
        };

        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Start_After_End_Should_Fail()
    {
        var request = new MarketDataOrchestratorRequest
        {
            Start = DateTimeOffset.Parse("2024-06-07"),
            End = DateTimeOffset.Parse("2024-06-03"),
            Timespans = [Timespan.minute]
        };

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage.Contains("Start must be before or equal to end"));
    }
}
