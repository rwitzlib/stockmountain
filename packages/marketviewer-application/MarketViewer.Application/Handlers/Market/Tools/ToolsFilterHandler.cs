using MarketViewer.Contracts.Interfaces;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Requests.Market;
using MarketViewer.Contracts.Requests.Tools;
using MarketViewer.Contracts.Responses.Tools;
using MarketViewer.Filters;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MarketViewer.Application.Handlers.Market.Tools;

public class ToolsFilterHandler(IMarketDataRepository repository, IndicatorExpressionEngine engine, ILogger<ToolsFilterHandler> logger)
{
    public async Task<ToolsFilterResponse> Handle(ToolsFilterRequest request, CancellationToken cancellationToken)
    {
        List<long> passingTimestamps = [];

        var stocksResponse = await repository.GetStockDataAsync(new StocksRequest
        {
            Ticker = request.Ticker,
            Multiplier = request.Multiplier,
            Timespan = request.Timespan,
            From = request.From,
            To = request.To,
        });

        if (stocksResponse.Results == null || !stocksResponse.Results.Any() || stocksResponse.Results.Count < 30)
        {
            return new ToolsFilterResponse
            {
                Results = [],
                MatchingTimestamps = passingTimestamps
            };
        }

        for (int i = 30; i < stocksResponse.Results.Count; i++)
        {
            var clonedResponse = stocksResponse.Clone();
            clonedResponse.Results = stocksResponse.Results.GetRange(0, i);
            bool passesFilter = false;
            foreach (var filter in request.Filters)
            {
                var expression = engine.ParseExpression(filter);

                // A bare line defaults to 1m here as in scans and backtests (plan 20, decision 6); a
                // [tf] suffix overrides it. The bars are still the chart's bars regardless.
                passesFilter = engine.EvaluateExpression(expression, clonedResponse, RangeSuffix.DefaultTimeframe);

                if (!passesFilter)
                {
                    break;
                }
            }
            if (passesFilter)
            {
                passingTimestamps.Add(stocksResponse.Results[i].Timestamp);
            }
        }

        return new ToolsFilterResponse
        {
            Results = stocksResponse.Results,
            MatchingTimestamps = passingTimestamps
        };
    }
}
