using FluentAssertions;
using MarketViewer.Contracts.Responses.Market;
using Polygon.Client.Models;
using Xunit;

namespace MarketDataAggregator.UnitTests;

public class AggregateMergerTests
{
    [Fact]
    public void Fresh_Bars_Win_Inside_Window_And_Existing_Bars_After_Window_Are_Preserved()
    {
        var fresh = new List<StocksResponse>
        {
            Response("SPY", Bar(100, close: 2f), Bar(150, close: 3f))
        };
        var existing = new List<StocksResponse>
        {
            Response("SPY", Bar(100, close: 1f), Bar(150, close: 1f), Bar(200, close: 4f), Bar(250, close: 5f))
        };

        var merged = AggregateMerger.Merge(fresh, existing, fetchWindowEnd: 150);

        var spy = merged.Single();
        spy.Results.Select(bar => bar.Timestamp).Should().Equal(100, 150, 200, 250);
        spy.Results[0].Close.Should().Be(2f);
        spy.Results[1].Close.Should().Be(3f);
        spy.Results[2].Close.Should().Be(4f);
        spy.Results[3].Close.Should().Be(5f);
    }

    [Fact]
    public void Ticker_Missing_From_Fresh_Fetch_Is_Preserved()
    {
        var fresh = new List<StocksResponse>
        {
            Response("SPY", Bar(100))
        };
        var existing = new List<StocksResponse>
        {
            Response("DELISTED", Bar(50), Bar(100))
        };

        var merged = AggregateMerger.Merge(fresh, existing, fetchWindowEnd: 100);

        merged.Should().HaveCount(2);
        merged.Single(response => response.Ticker == "DELISTED").Results.Should().HaveCount(2);
    }

    [Fact]
    public void Ticker_Only_In_Fresh_Fetch_Is_Kept()
    {
        var fresh = new List<StocksResponse>
        {
            Response("NEWIPO", Bar(100))
        };

        var merged = AggregateMerger.Merge(fresh, [], fetchWindowEnd: 100);

        merged.Should().ContainSingle(response => response.Ticker == "NEWIPO");
    }

    [Fact]
    public void Existing_Response_With_Null_Results_Does_Not_Throw()
    {
        var fresh = new List<StocksResponse>
        {
            Response("SPY", Bar(100))
        };
        var existing = new List<StocksResponse>
        {
            new() { Ticker = "SPY", Results = null! }
        };

        var merged = AggregateMerger.Merge(fresh, existing, fetchWindowEnd: 100);

        merged.Single().Results.Should().HaveCount(1);
    }

    private static StocksResponse Response(string ticker, params Bar[] bars)
    {
        return new StocksResponse
        {
            Ticker = ticker,
            Status = "OK",
            Results = bars.ToList()
        };
    }

    private static Bar Bar(long timestamp, float close = 1f)
    {
        return new Bar
        {
            Timestamp = timestamp,
            Close = close
        };
    }
}
