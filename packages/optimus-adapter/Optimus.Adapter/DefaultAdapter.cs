using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Records;
using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Enums;
using Massive.Client.Interfaces;
using Optimus.Adapter.Interfaces;
using Microsoft.Extensions.Logging;
using Optimus.Infrastructure.Repositories;

namespace Optimus.Adapter;

public class DefaultAdapter(
    TradeRepository tradeRepository,
    IMassiveClient massiveClient,
    ILogger<DefaultAdapter> logger) : IAdapter
{

    public async Task<BuyResult> Buy(StrategyDto strategy, string ticker)
    {
        try
        {
            var currentPrice = await GetSnapshotPrice(ticker);

            if (currentPrice is null)
            {
                logger.LogWarning("No price data returned for {ticker}.", ticker);
                return BuyResult.Failed($"No price data available for {ticker}");
            }

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

            var actualEntryCost = shares * currentPrice.Value;
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
                EntryPrice = currentPrice.Value,
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
            var closePrice = await GetSnapshotPrice(trade.Ticker);

            if (closePrice is null)
            {
                logger.LogWarning("No price data returned for {ticker}.", trade.Ticker);
                return SellResult.Failed($"No price data available for {trade.Ticker}");
            }

            var closePosition = closePrice.Value * trade.Shares;
            var profit = closePosition - trade.EntryPosition;

            trade.ClosePrice = closePrice.Value;
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

    /// <summary>
    /// Fetches the latest minute-bar close from the Massive snapshot API.
    /// Returns null when the ticker is missing from the snapshot (e.g. halted) or has no price yet.
    /// </summary>
    private async Task<float?> GetSnapshotPrice(string ticker)
    {
        var response = await massiveClient.GetAllTickersSnapshot(ticker);

        var minuteClose = response?.Tickers?
            .FirstOrDefault(s => string.Equals(s.Ticker, ticker, StringComparison.OrdinalIgnoreCase))?
            .Minute?.Close;

        return minuteClose > 0 ? minuteClose : null;
    }
}
