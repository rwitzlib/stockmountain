using MarketViewer.Contracts.Requests.Market;
using MarketViewer.Contracts.Responses.Market;
using Massive.Client.Requests;
using Massive.Client.Responses;

namespace MarketViewer.Infrastructure.Mapping;

public static class AggregateMapper
{
    public static MassiveAggregateRequest ToMassiveRequest(StocksRequest request) =>
        new()
        {
            Ticker = request.Ticker.ToUpperInvariant(),
            Multiplier = request.Multiplier,
            Timespan = request.Timespan.ToString().ToLowerInvariant(),
            From = request.From.ToUnixTimeMilliseconds().ToString(),
            To = request.To.ToUnixTimeMilliseconds().ToString(),
            Adjusted = true,
            Sort = "asc",
            Limit = request.Limit
        };

    public static StocksResponse ToStocksResponse(MassiveAggregateResponse response) =>
        new()
        {
            Ticker = response.Ticker,
            Status = response.Status,
            Results = response.Results?.ToList()
        };
}
