using FluentAssertions;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Requests.Market;
using MarketViewer.Infrastructure.Mapping;
using Massive.Client.Models;
using Massive.Client.Responses;
using Xunit;

namespace MarketViewer.Infrastructure.UnitTests.Mappings;

public class AggregateMapperUnitTests
{
    [Fact]
    public void ToMassiveRequest_Maps_StocksRequest_Fields()
    {
        var from = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero);
        var request = new StocksRequest
        {
            Ticker = "aapl",
            Multiplier = 5,
            Timespan = Timespan.minute,
            From = from,
            To = to,
            Limit = 1000
        };

        var result = AggregateMapper.ToMassiveRequest(request);

        result.Ticker.Should().Be("AAPL");
        result.Multiplier.Should().Be(5);
        result.Timespan.Should().Be("minute");
        result.From.Should().Be(from.ToUnixTimeMilliseconds().ToString());
        result.To.Should().Be(to.ToUnixTimeMilliseconds().ToString());
        result.Adjusted.Should().BeTrue();
        result.Sort.Should().Be("asc");
        result.Limit.Should().Be(1000);
    }

    [Fact]
    public void ToStocksResponse_Maps_MassiveAggregateResponse_Fields()
    {
        var response = new MassiveAggregateResponse
        {
            Ticker = "AAPL",
            Status = "OK",
            Results = [new Bar { Open = 1, Close = 2 }]
        };

        var result = AggregateMapper.ToStocksResponse(response);

        result.Ticker.Should().Be("AAPL");
        result.Status.Should().Be("OK");
        result.Results.Should().HaveCount(1);
        result.Indicators.Should().BeNull();
        result.TickerInfo.Should().NotBeNull();
    }
}
