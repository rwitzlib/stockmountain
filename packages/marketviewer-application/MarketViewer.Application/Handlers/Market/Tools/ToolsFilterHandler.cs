using MarketViewer.Contracts.Interfaces;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Requests.Market;
using MarketViewer.Contracts.Requests.Tools;
using MarketViewer.Contracts.Responses.Tools;
using MarketViewer.Filters;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MarketViewer.Application.Handlers.Market.Tools;

public class ToolsFilterHandler(IMarketDataRepository repository, IndicatorExpressionEngine engine, ILogger<ToolsFilterHandler> logger) : IRequestHandler<ToolsFilterRequest, ToolsFilterResponse>
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

                passesFilter = engine.EvaluateExpression(expression, clonedResponse, new Timeframe(request.Multiplier, request.Timespan));

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
