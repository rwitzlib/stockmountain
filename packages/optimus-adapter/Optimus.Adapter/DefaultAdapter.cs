using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Records;
using Amazon.SimpleNotificationService;
using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Requests.Market;
using MarketViewer.Contracts.Enums;
using Optimus.Adapter.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using Optimus.Infrastructure.Repositories;

namespace Optimus.Adapter;

public class DefaultAdapter(
    TradeRepository tradeRepository,
    HttpClient httpClient,
    ILogger<DefaultAdapter> logger) : IAdapter
{

    public async Task<BuyResult> Buy(StrategyDto strategy, string ticker)
    {
        try
        {
            var stocksResponse = await httpClient.PostAsJsonAsync("api/stocks", new StocksRequest
            {
                Ticker = ticker,
                Multiplier = 1,
                Timespan = Timespan.minute,
                From = DateTimeOffset.Now.Date,
                To = DateTimeOffset.Now.Date,
                Limit = 5
            });

            if (!stocksResponse.IsSuccessStatusCode)
            {
                logger.LogError("Error getting price for {ticker}.", ticker);
                return BuyResult.Failed($"Failed to get price for {ticker}");
            }

            var response = await stocksResponse.Content.ReadFromJsonAsync<StocksResponse>();

            if (response?.Results == null || response.Results.Count == 0)
            {
                logger.LogWarning("No price data returned for {ticker}.", ticker);
                return BuyResult.Failed($"No price data available for {ticker}");
            }

            var currentPrice = response.Results.Last().Close;
            var shares = strategy.PositionSettings.Model.Type switch
            {
                PositionType.Fixed => (int)(strategy.PositionSettings.Model.Size / currentPrice),
                //PositionType.Percentage => (int)(strategy.PositionSettings.Model.PositionSize / currentPrice),
                _ => 0
            };

            if (shares <= 0)
            {
                logger.LogInformation("Could not afford position: {ticker}.", ticker);
                return BuyResult.Failed($"Could not afford any shares of {ticker} at ${currentPrice}");
            }

            var actualEntryCost = shares * currentPrice;
            var tradeId = Guid.NewGuid().ToString();

            var record = new TradeRecord
            {
                Id = tradeId,
                UserId = strategy.UserId,
                StrategyId = strategy.Id,
                Ticker = ticker,
                Type = TradeType.Paper,
                OrderStatus = TradeStatus.Open,
                OpenedAt = DateTimeOffset.Now.ToString(),
                EntryPrice = currentPrice,
                EntryPosition = actualEntryCost,
                Shares = shares
            };

            var isSuccess = await tradeRepository.Put(record);

            if (!isSuccess)
            {
                logger.LogInformation("Failed to create trade record for: {ticker} using strategy: {strategy}.", ticker, strategy.Id);
                return BuyResult.Failed($"Failed to persist trade record for {ticker}");
            }

            logger.LogInformation(
                "Trade opened for: {ticker} using strategy {strategy}. Shares: {shares}, Cost: {cost}",
                ticker, strategy.Id, shares, actualEntryCost);

            return BuyResult.Success((decimal)actualEntryCost, tradeId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Exception while buying position: {message}", e.Message);
            return BuyResult.Failed($"Exception: {e.Message}");
        }
    }

    public async Task<SellResult> Sell(TradeRecord trade)
    {
        try
        {
            var stocksResponse = await httpClient.PostAsJsonAsync("api/stocks", new StocksRequest
            {
                Ticker = trade.Ticker,
                Multiplier = 1,
                Timespan = Timespan.minute,
                From = DateTimeOffset.Now.Date,
                To = DateTimeOffset.Now.Date,
                Limit = 5
            });

            if (!stocksResponse.IsSuccessStatusCode)
            {
                logger.LogError("Error getting price for {ticker}.", trade.Ticker);
                return SellResult.Failed($"Failed to get price for {trade.Ticker}");
            }

            var response = await stocksResponse.Content.ReadFromJsonAsync<StocksResponse>();

            if (response?.Results == null || response.Results.Count == 0)
            {
                logger.LogWarning("No price data returned for {ticker}.", trade.Ticker);
                return SellResult.Failed($"No price data available for {trade.Ticker}");
            }

            var closePrice = response.Results.Last().Close;
            var closePosition = closePrice * trade.Shares;
            var profit = closePosition - trade.EntryPosition;

            trade.ClosePrice = closePrice;
            trade.ClosePosition = closePosition;
            trade.Profit = profit;
            trade.OrderStatus = TradeStatus.Closed;
            trade.ClosedAt = DateTimeOffset.Now.ToString();

            var isSuccess = await tradeRepository.Put(trade);

            if (!isSuccess)
            {
                logger.LogInformation("Failed to close trade record for: {ticker}.", trade.Ticker);
                return SellResult.Failed($"Failed to persist close for {trade.Ticker}");
            }

            logger.LogInformation(
                "Trade closed for: {ticker}. CloseValue: {closeValue}, Profit: {profit}",
                trade.Ticker, closePosition, profit);

            return SellResult.Success((decimal)closePosition, (decimal)profit);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Exception while selling position: {message}", e.Message);
            return SellResult.Failed($"Exception: {e.Message}");
        }
    }

    public float GetPrice(string ticker)
    {
        throw new NotImplementedException();
    }
}
