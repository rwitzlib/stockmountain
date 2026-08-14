using Alpaca.Client.Interfaces;
using Alpaca.Client.Models;
using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Enums.Backtest;
using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Records;
using Massive.Client.Interfaces;
using Microsoft.Extensions.Logging;
using Optimus.Adapter.Config;
using Optimus.Adapter.Interfaces;
using Optimus.Infrastructure.Repositories;
using System.Globalization;

namespace Optimus.Adapter;

/// <summary>
/// Broker adapter for Alpaca. One instance per environment (paper/live) — the injected
/// client and the TradeType stamped on records are the only differences between tiers.
///
/// Order lifecycle: every order is submitted with a deterministic client_order_id derived
/// from the trade id, polled to a fill, and canceled on timeout (a market order on a
/// halted ticker sits unfilled forever). Every filled buy is followed by a GTC stop-market
/// backstop far below the logical stop; it exists to bound loss when the bot is dead and
/// must be canceled before any normal sell.
///
/// Trade records carry actual broker fill prices/quantities and real fill timestamps. On a
/// delayed market-data plan this makes timed exits (evaluated against the data clock) run
/// up to DelayMinutes long — accepted for the dress-rehearsal tier; the parity tier is
/// DefaultAdapter.
/// </summary>
public class AlpacaAdapter(
    IAlpacaTradingClient alpacaClient,
    TradeType tradeType,
    TradeRepository tradeRepository,
    IMassiveClient massiveClient,
    AlpacaAdapterConfig config,
    ILogger<AlpacaAdapter> logger) : IAdapter
{
    public async Task<BuyResult> Buy(StrategyDto strategy, string ticker)
    {
        try
        {
            var currentPrice = await GetSnapshotPrice(ticker);

            if (currentPrice is null)
            {
                logger.LogWarning("No price data returned for {Ticker}.", ticker);
                return BuyResult.Failed($"No price data available for {ticker}");
            }

            var shares = strategy.PositionSettings.Model.Type switch
            {
                PositionType.Fixed => AlpacaOrderMath.ComputeShares(strategy.PositionSettings.Model.Size, currentPrice.Value),
                _ => 0
            };

            if (shares <= 0)
            {
                logger.LogInformation("Could not afford position: {Ticker}.", ticker);
                return BuyResult.Failed($"Could not afford any shares of {ticker} at ${currentPrice}");
            }

            var tradeId = Guid.NewGuid().ToString();

            var order = await alpacaClient.SubmitOrder(new AlpacaOrderRequest
            {
                Symbol = ticker,
                Qty = shares.ToString(CultureInfo.InvariantCulture),
                Side = "buy",
                Type = "market",
                TimeInForce = "day",
                ClientOrderId = $"{tradeId}:entry"
            });

            if (order is null)
            {
                return BuyResult.Failed($"Alpaca rejected buy order for {ticker}");
            }

            var settled = await WaitForFill(order.Id);

            if (settled is null || !settled.IsFilled)
            {
                settled = await CancelAndSettle(order.Id);
            }

            if (settled is null || settled.FilledShares <= 0 || settled.FilledPrice is null)
            {
                logger.LogError(
                    "Buy order {OrderId} for {Ticker} did not fill within {Timeout}s and was canceled (status: {Status}).",
                    order.Id, ticker, config.FillTimeoutSeconds, settled?.Status ?? "unknown");
                return BuyResult.Failed($"Buy order for {ticker} did not fill in time");
            }

            // A cancel that raced a partial fill leaves real shares behind; track exactly
            // what filled rather than pretending the order failed.
            if (settled.FilledShares < shares)
            {
                logger.LogWarning(
                    "Buy order {OrderId} for {Ticker} partially filled: {Filled}/{Requested} shares.",
                    order.Id, ticker, settled.FilledShares, shares);
            }

            var fillPrice = settled.FilledPrice.Value;
            var filledShares = settled.FilledShares;
            var entryCost = fillPrice * filledShares;

            var record = new TradeRecord
            {
                Id = tradeId,
                UserId = strategy.UserId,
                StrategyId = strategy.Id,
                Ticker = ticker,
                Type = tradeType,
                OrderStatus = TradeStatus.Open,
                OpenedAt = (settled.FilledAt ?? DateTimeOffset.UtcNow).ToString(),
                EntryPrice = fillPrice,
                EntryPosition = entryCost,
                Shares = filledShares,
                EntryOrderId = order.Id
            };

            record.BackstopOrderId = await PlaceBackstop(strategy, record);

            var persisted = await tradeRepository.Put(record);

            if (!persisted)
            {
                // The position exists at the broker with no record on our side. Startup
                // reconciliation (Phase 4) flags exactly this shape; make it findable.
                logger.LogCritical(
                    "Filled Alpaca order {OrderId} for {Ticker} ({Shares} shares) has NO trade record — broker position is untracked until reconciliation.",
                    order.Id, ticker, filledShares);
                return BuyResult.Failed($"Failed to persist trade record for {ticker}");
            }

            logger.LogInformation(
                "Alpaca trade opened for {Ticker} using strategy {StrategyId}. Shares: {Shares}, FillPrice: {FillPrice}, Cost: {Cost}, Backstop: {BackstopOrderId}",
                ticker, strategy.Id, filledShares, fillPrice, entryCost, record.BackstopOrderId ?? "none");

            return BuyResult.Success((decimal)entryCost, tradeId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Exception while buying {Ticker}: {Message}", ticker, e.Message);
            return BuyResult.Failed($"Exception: {e.Message}");
        }
    }

    // triggerPrice is unused: real positions close at the broker's actual fill.
    public async Task<SellResult> Sell(TradeRecord trade, float? triggerPrice)
    {
        try
        {
            // The backstop must be off the books before a normal sell, or both could fill
            // and leave the account short.
            AlpacaOrder backstop = null;

            if (!string.IsNullOrEmpty(trade.BackstopOrderId))
            {
                backstop = await alpacaClient.GetOrder(trade.BackstopOrderId);

                if (backstop is null)
                {
                    // Unknown backstop state: selling now could double-fill if the stop
                    // still rests. Skip this tick; the next one retries.
                    return SellResult.Failed(
                        $"Could not fetch backstop {trade.BackstopOrderId} for {trade.Ticker}; aborting sell");
                }

                if (backstop.IsFilled)
                {
                    return await RecordClose(trade, backstop, closedByBackstop: true);
                }

                if (!backstop.IsTerminal)
                {
                    var cancelResult = await alpacaClient.CancelOrder(trade.BackstopOrderId);

                    if (cancelResult == CancelOrderResult.NotCancelable)
                    {
                        // The backstop filled between the status check and the cancel.
                        backstop = await alpacaClient.GetOrder(trade.BackstopOrderId);

                        if (backstop is { IsFilled: true })
                        {
                            return await RecordClose(trade, backstop, closedByBackstop: true);
                        }
                    }
                    else if (cancelResult == CancelOrderResult.Failed)
                    {
                        return SellResult.Failed(
                            $"Could not cancel backstop {trade.BackstopOrderId} for {trade.Ticker}; aborting sell to avoid a double fill");
                    }
                }
            }

            var order = await alpacaClient.SubmitOrder(new AlpacaOrderRequest
            {
                Symbol = trade.Ticker,
                Qty = trade.Shares.ToString(CultureInfo.InvariantCulture),
                Side = "sell",
                Type = "market",
                TimeInForce = "day",
                ClientOrderId = $"{trade.Id}:close"
            });

            if (order is null)
            {
                await ReplaceBackstop(trade, backstop);
                return SellResult.Failed($"Alpaca rejected sell order for {trade.Ticker}");
            }

            var settled = await WaitForFill(order.Id);

            if (settled is null || !settled.IsFilled)
            {
                settled = await CancelAndSettle(order.Id);
            }

            if (settled is null || !settled.IsFilled)
            {
                if (settled?.FilledShares > 0)
                {
                    // Partially closed and canceled: the record no longer matches the broker.
                    // Leave it open and loud; reconciliation resolves the quantity mismatch.
                    logger.LogCritical(
                        "Sell order {OrderId} for {Ticker} partially filled {Filled}/{Requested} then canceled — broker and record disagree until reconciliation.",
                        order.Id, trade.Ticker, settled.FilledShares, trade.Shares);
                }
                else
                {
                    await ReplaceBackstop(trade, backstop);
                }

                return SellResult.Failed($"Sell order for {trade.Ticker} did not fill in time");
            }

            return await RecordClose(trade, settled, closedByBackstop: false);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Exception while selling {Ticker}: {Message}", trade.Ticker, e.Message);
            return SellResult.Failed($"Exception: {e.Message}");
        }
    }

    public float GetPrice(string ticker)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Places the GTC stop-market disaster backstop for a freshly filled position.
    /// Returns the order id, or null when placement failed — the buy still stands
    /// (unwinding a filled entry is worse than a temporarily unprotected position),
    /// but the error is loud and reconciliation re-places missing backstops.
    /// </summary>
    private async Task<string> PlaceBackstop(StrategyDto strategy, TradeRecord record)
    {
        var stopPrice = AlpacaOrderMath.ComputeBackstopStopPrice(
            record.EntryPrice, record.Shares, strategy.ExitSettings?.StopLoss, config);

        if (stopPrice is null)
        {
            logger.LogError("Could not compute backstop stop price for {Ticker}; position is unprotected.", record.Ticker);
            return null;
        }

        var backstop = await alpacaClient.SubmitOrder(new AlpacaOrderRequest
        {
            Symbol = record.Ticker,
            Qty = record.Shares.ToString(CultureInfo.InvariantCulture),
            Side = "sell",
            Type = "stop",
            TimeInForce = "gtc",
            StopPrice = stopPrice.Value.ToString(CultureInfo.InvariantCulture),
            ClientOrderId = $"{record.Id}:backstop"
        });

        if (backstop is null)
        {
            logger.LogError(
                "Failed to place backstop for {Ticker} at {StopPrice}; position is unprotected until reconciliation re-places it.",
                record.Ticker, stopPrice);
            return null;
        }

        return backstop.Id;
    }

    /// <summary>
    /// Restores protection after a sell attempt died between canceling the old backstop and
    /// filling: re-submits a stop at the same price under a fresh idempotency id.
    /// </summary>
    private async Task ReplaceBackstop(TradeRecord trade, AlpacaOrder canceledBackstop)
    {
        if (canceledBackstop?.StopPrice is null)
        {
            return;
        }

        var replacement = await alpacaClient.SubmitOrder(new AlpacaOrderRequest
        {
            Symbol = trade.Ticker,
            Qty = trade.Shares.ToString(CultureInfo.InvariantCulture),
            Side = "sell",
            Type = "stop",
            TimeInForce = "gtc",
            StopPrice = canceledBackstop.StopPrice,
            ClientOrderId = $"{trade.Id}:r{Guid.NewGuid():N}"[..44]
        });

        if (replacement is null)
        {
            logger.LogError(
                "Failed to re-place backstop for {Ticker} after aborted sell; position is unprotected until reconciliation.",
                trade.Ticker);
            return;
        }

        trade.BackstopOrderId = replacement.Id;

        if (!await tradeRepository.Put(trade))
        {
            logger.LogError(
                "Re-placed backstop {OrderId} for {Ticker} but failed to persist it on the trade record.",
                replacement.Id, trade.Ticker);
        }
    }

    /// <summary>
    /// Persists the close from an actual broker fill — either the normal market sell or
    /// the backstop, whichever flattened the position.
    /// </summary>
    private async Task<SellResult> RecordClose(TradeRecord trade, AlpacaOrder fill, bool closedByBackstop)
    {
        if (fill.FilledPrice is null)
        {
            return SellResult.Failed($"Fill for {trade.Ticker} has no price");
        }

        if (closedByBackstop)
        {
            logger.LogWarning(
                "Backstop {OrderId} fired for {Ticker} — position was flattened at the broker before the bot sold.",
                fill.Id, trade.Ticker);
            trade.ExitReason = BacktestExitReason.stopLoss;
        }

        var closePrice = fill.FilledPrice.Value;
        var closePosition = closePrice * trade.Shares;
        var profit = closePosition - trade.EntryPosition;

        trade.ClosePrice = closePrice;
        trade.ClosePosition = closePosition;
        trade.Profit = profit;
        trade.OrderStatus = TradeStatus.Closed;
        trade.ClosedAt = (fill.FilledAt ?? DateTimeOffset.UtcNow).ToString();
        trade.CloseOrderId = fill.Id;

        if (!await tradeRepository.Put(trade))
        {
            logger.LogError("Failed to persist close for {Ticker}.", trade.Ticker);
            return SellResult.Failed($"Failed to persist close for {trade.Ticker}");
        }

        logger.LogInformation(
            "Alpaca trade closed for {Ticker}. ClosePrice: {ClosePrice}, CloseValue: {CloseValue}, Profit: {Profit}, ClosedByBackstop: {ClosedByBackstop}",
            trade.Ticker, closePrice, closePosition, profit, closedByBackstop);

        return SellResult.Success((decimal)closePosition, (decimal)profit);
    }

    /// <summary>Polls the order until it reaches a terminal state or the fill timeout lapses.</summary>
    private async Task<AlpacaOrder> WaitForFill(string orderId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(config.FillTimeoutSeconds);
        AlpacaOrder order = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            order = await alpacaClient.GetOrder(orderId);

            if (order is { IsTerminal: true })
            {
                return order;
            }

            await Task.Delay(config.FillPollIntervalMs);
        }

        return order;
    }

    /// <summary>
    /// Cancels an order that outlived the fill timeout, then waits briefly for it to settle.
    /// The returned order is the final observed state — a cancel can race a fill, so callers
    /// must re-check FilledShares rather than assume the cancel won.
    /// </summary>
    private async Task<AlpacaOrder> CancelAndSettle(string orderId)
    {
        await alpacaClient.CancelOrder(orderId);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(config.CancelSettleTimeoutSeconds);
        AlpacaOrder order = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            order = await alpacaClient.GetOrder(orderId);

            if (order is { IsTerminal: true })
            {
                return order;
            }

            await Task.Delay(500);
        }

        return order;
    }

    /// <summary>
    /// Latest minute-bar close from the Massive snapshot API — the same sizing price source
    /// as DefaultAdapter, so both tiers size positions identically. Fill prices come from
    /// Alpaca; this price only determines the share count.
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
