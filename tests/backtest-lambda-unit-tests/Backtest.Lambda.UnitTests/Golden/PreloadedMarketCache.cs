using Amazon.S3;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using Massive.Client.Models;
using Massive.Client.Responses;
using Microsoft.Extensions.Caching.Memory;
using System.Net;

namespace Backtest.Lambda.UnitTests.Golden;

/// <summary>
/// An <see cref="IMarketCache"/> for golden tests: behaves like <see cref="MemoryMarketCache"/> but
/// <see cref="Initialize"/> never touches S3 — it just returns whatever was preloaded with
/// <see cref="Preload"/> under the same (timeframe, date) key the backtester will ask for, and
/// throws the same NotFound <see cref="AmazonS3Exception"/> S3 does for a file that was never
/// written (weekend, holiday, before the bucket's first day).
/// Mirrors what the real cache holds after downloading the market-data files:
///   1-minute → one file per day; 1-hour → one file per month; 1-day → one file per year.
/// </summary>
internal sealed class PreloadedMarketCache : IMarketCache
{
    private readonly MemoryMarketCache _inner = new(new MemoryCache(new MemoryCacheOptions()), null);
    private readonly Dictionary<string, List<StocksResponse>> _preloaded = new();

    private static string Key(Timeframe tf, DateTimeOffset date) => $"{tf.Multiplier}/{tf.Timespan}/{date.Date:yyyyMMdd}";

    public void Preload(Timeframe timeframe, DateTimeOffset date, params StocksResponse[] responses)
    {
        _preloaded[Key(timeframe, date)] = responses.ToList();
    }

    public Task<IEnumerable<StocksResponse>> Initialize(DateTimeOffset date, Timeframe timeframe)
    {
        if (!_preloaded.TryGetValue(Key(timeframe, date), out var responses))
        {
            throw new AmazonS3Exception($"No preloaded market data for {timeframe.Multiplier}/{timeframe.Timespan} @ {date:yyyy-MM-dd}") { StatusCode = HttpStatusCode.NotFound };
        }

        SetTickersByTimeframe(date, timeframe, responses.Select(r => r.Ticker).ToList());
        foreach (var response in responses)
        {
            SetStocksResponse(response, timeframe, date);
        }
        return Task.FromResult<IEnumerable<StocksResponse>>(responses);
    }

    public IEnumerable<string> GetTickers() => _inner.GetTickers();
    public void SetTickers(IEnumerable<string> tickers) => _inner.SetTickers(tickers);
    public IEnumerable<string> GetTickersByTimeframe(Timeframe timeframe, DateTimeOffset timestamp) => _inner.GetTickersByTimeframe(timeframe, timestamp);
    public void SetTickersByTimeframe(DateTimeOffset date, Timeframe timeframe, IEnumerable<string> tickers) => _inner.SetTickersByTimeframe(date, timeframe, tickers);
    public StocksResponse GetStocksResponse(string ticker, Timeframe timeframe, DateTimeOffset timestamp) => _inner.GetStocksResponse(ticker, timeframe, timestamp);
    public void SetStocksResponse(StocksResponse stocksResponse, Timeframe timeframe, DateTimeOffset date) => _inner.SetStocksResponse(stocksResponse, timeframe, date);
    public TickerDetails GetTickerDetails(string ticker) => _inner.GetTickerDetails(ticker);
    public void SetTickerDetails(TickerDetails tickerDetails) => _inner.SetTickerDetails(tickerDetails);
    public void AddLiveBar(MassiveWebsocketAggregateResponse bar) => _inner.AddLiveBar(bar);
    public Bar GetLiveBar(string ticker) => _inner.GetLiveBar(ticker);
    public IReadOnlyList<Bar> GetRecentLiveBars(string ticker) => _inner.GetRecentLiveBars(ticker);
}
