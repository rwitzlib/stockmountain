using MarketViewer.Application.Services;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Models.Indicator;
using MarketViewer.Contracts.Responses.Market;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MarketViewer.Application.Handlers.Tools;

public class PerformanceHandler(
    IMarketCache marketCache,
    IIndicatorCalculationService indicatorCalculationService,
    IGpuSmaCalculationService gpuSmaCalculationService)
{
    public async Task Handle(string type)
    {
        var tickers = marketCache.GetTickers();
        var smaIndicator = new Indicator
        {
            Type = StudyType.sma,
            Parameters = ["9"]
        };
        var period = int.Parse(smaIndicator.Parameters[0]);
        var timeframe = new Timeframe(1, Timespan.minute);

        if (type?.ToLowerInvariant() == "gpu")
        {
            // GPU path: batch process all tickers
            await HandleGpu(tickers, period, timeframe);
        }
        else
        {
            // CPU path: sequential processing
            await HandleCpu(tickers, smaIndicator, timeframe);
        }
    }

    private async Task HandleCpu(
        IEnumerable<string> tickers,
        Indicator smaIndicator,
        Timeframe timeframe)
    {
        Parallel.ForEach(tickers, ticker =>
        {
            var response = marketCache.GetStocksResponse(ticker, timeframe, DateTimeOffset.Now);
            if (response != null)
            {
                var studyResponse = indicatorCalculationService.Compute(smaIndicator, response, timeframe);
            }
        });
    }

    private async Task HandleGpu(
        IEnumerable<string> tickers,
        int period,
        Timeframe timeframe)
    {
        // Collect all ticker data in parallel
        var tickerData = new ConcurrentDictionary<string, StocksResponse>();
        
        await Parallel.ForEachAsync(tickers, async (ticker, cancellationToken) =>
        {
            var response = marketCache.GetStocksResponse(ticker, timeframe, DateTimeOffset.Now);
            if (response != null)
            {
                tickerData[ticker] = response;
            }
            await Task.CompletedTask;
        });

        // Batch process all tickers on GPU
        var results = gpuSmaCalculationService.CalculateSmaBatch(
            new Dictionary<string, StocksResponse>(tickerData), 
            period);

        // Results are now available in the dictionary
        // You can process them further if needed
        _ = results.Count;
    }
}
