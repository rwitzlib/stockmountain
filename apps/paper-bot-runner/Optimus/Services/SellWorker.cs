using Alpaca.Client;
using MarketViewer.Clients.Interfaces;
using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Records;
using MarketViewer.Contracts.Records.Strategy;
using MarketViewer.Contracts.Requests.Market;
using Massive.Client.Interfaces;
using Optimus.Adapter;
using Optimus.Infrastructure.Repositories;
using Optimus.Infrastructure.Services;
using Quartz;

namespace Optimus.Services;

[DisallowConcurrentExecution]
public class SellWorker(
    StrategyRepository strategyRepository,
    StrategyStateRepository stateRepository,
    TradeRepository tradeRepository,
    IMassiveClient massiveClient,
    ILivePriceClient livePriceClient,
    MarketCalendarService marketCalendar,
    AdapterFactory adapterFactory,
    MarketDataClock dataClock,
    ILogger<SellWorker> logger) : IJob
{
    private const int SnapshotBatchSize = 500;

    // Live prices older than this (in data time) mean the feed missed the ticker;
    // fall back to the snapshot rather than exit-evaluate against a dead quote.
    private const int MaxLivePriceAgeMinutes = 5;

    public async Task Execute(IJobExecutionContext context)
    {
        // Gate on the data clock, not the wall clock: on a delayed plan the snapshot
        // prices lag by DelayMinutes, so exits keep evaluating past the wall-clock close
        // until the data covering the full session has arrived. Without this the last
        // DelayMinutes of each session were never exit-evaluated until the next open.
        var dataNow = dataClock.Now;

        if (!await marketCalendar.IsMarketOpen(asOf: dataNow))
        {
            return;
        }

        try
        {
            var strategies = await strategyRepository.ListAll();
            var enabledStrategies = strategies.Where(q => q.State == StrategyStateType.Active).ToList();

            if (enabledStrategies.Count == 0)
            {
                return;
            }

            // Gather every open position up front so prices can be fetched in one batch
            // instead of one request per position.
            var openPositions = new List<(StrategyDto Strategy, TradeRecord Trade)>();
            foreach (var strategy in enabledStrategies)
            {
                var trades = await tradeRepository.ListTradesByStrategy(strategy.Id, null, TradeStatus.Open);
                openPositions.AddRange(trades.Select(trade => (strategy, trade)));
            }

            if (openPositions.Count == 0)
            {
                return;
            }

            var distinctTickers = openPositions.Select(p => p.Trade.Ticker).Distinct().ToList();
            var priceMap = await GetCurrentPrices(distinctTickers, dataNow);

            var sellTasks = openPositions.Select(p => SellPositionIfApplicable(p.Strategy, p.Trade, priceMap, dataNow));
            await Task.WhenAll(sellTasks);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error in SellWorker: {Message}", e.Message);
        }
    }

    /// <summary>
    /// Fetches current prices for all tickers: the forming websocket bar from the API
    /// first, batched snapshot calls as fallback. Tickers missing from both (e.g.
    /// halted) are simply absent from the map.
    /// </summary>
    private async Task<Dictionary<string, float>> GetCurrentPrices(List<string> tickers, DateTimeOffset dataNow)
    {
        var prices = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        // Prefer the forming bar: it moves tick-by-tick (in data time), so stops fire
        // when price crosses the threshold instead of up to a minute later on the
        // completed snapshot bar. Prices are canonical intra-minute; only volume
        // undercounts (ADR 0003), which exits don't read.
        var liveResponse = await livePriceClient.GetPricesAsync(new LivePricesRequest { Tickers = tickers });
        var staleCutoffMs = dataNow.AddMinutes(-MaxLivePriceAgeMinutes).ToUnixTimeMilliseconds();

        foreach (var livePrice in liveResponse?.Prices ?? [])
        {
            if (livePrice.Price > 0 && livePrice.Timestamp >= staleCutoffMs)
            {
                prices[livePrice.Ticker] = livePrice.Price;
            }
        }

        // Snapshot fallback: the close of the last completed delayed minute bar —
        // covers thin tickers the live feed hasn't seen recently and API outages.
        var missingTickers = tickers.Where(ticker => !prices.ContainsKey(ticker)).ToList();

        foreach (var chunk in missingTickers.Chunk(SnapshotBatchSize))
        {
            var response = await massiveClient.GetAllTickersSnapshot(string.Join(',', chunk));

            foreach (var snapshot in response?.Tickers ?? [])
            {
                if (snapshot.Ticker is not null && snapshot.Minute?.Close > 0)
                {
                    prices[snapshot.Ticker] = snapshot.Minute.Close;
                }
            }
        }

        if (prices.Count < tickers.Count)
        {
            logger.LogWarning("Missing prices for {MissingCount}/{TotalCount} open tickers",
                tickers.Count - prices.Count, tickers.Count);
        }

        return prices;
    }

    private async Task SellPositionIfApplicable(StrategyDto strategy, TradeRecord trade, Dictionary<string, float> priceMap, DateTimeOffset dataNow)
    {
        float? currentPrice = priceMap.TryGetValue(trade.Ticker, out var price) ? price : null;

        // TODO: Evaluate ExitSettings.ConditionalExit (scan-based exits) here once supported.
        // Timed exits compare against the data clock, consistent with the data-time OpenedAt stamp.
        var exitReason = ExitEvaluator.Evaluate(strategy, trade, currentPrice, dataNow);

        if (exitReason is null)
        {
            return;
        }

        logger.LogInformation("{ExitReason} hit for {Ticker} (strategy {StrategyId})",
            exitReason, trade.Ticker, strategy.Id);

        // The adapter persists this record on close, so the reason rides along with the fill.
        trade.ExitReason = exitReason;

        var adapter = adapterFactory.GetAdaptor(strategy.Integration);
        var sellResult = await adapter.Sell(trade, currentPrice);

        if (!sellResult.IsSuccess)
        {
            logger.LogWarning(
                "Sell failed for strategy {StrategyId}, ticker {Ticker}: {Reason}",
                strategy.Id, trade.Ticker, sellResult.FailureReason);
            return;
        }

        await UpdateStateOnClose(strategy, trade, sellResult);
    }

    /// <summary>
    /// Updates strategy state after a position is closed.
    /// Increments cash balance with close value, removes ticker from open set, sets cooldown.
    /// The repository update is atomic arithmetic (no version lock), so parallel closes in the
    /// same tick all land — a version condition here used to silently drop cash credits.
    /// </summary>
    private async Task UpdateStateOnClose(StrategyDto strategy, TradeRecord trade, Adapter.Interfaces.SellResult sellResult)
    {
        try
        {
            // Lazy-create state for strategies that predate state tracking.
            var startingBalance = (decimal)strategy.PositionSettings.StartingBalance;
            var existingState = await stateRepository.GetOrCreateState(strategy.Id, startingBalance);

            if (existingState == null)
            {
                logger.LogError("Failed to get state for strategy {StrategyId} during close", strategy.Id);
                return;
            }

            var closeValue = sellResult.ActualCloseValue;
            var entryCost = (decimal)trade.EntryPosition;
            var cooldownExpiry = TradeExecutionService.ComputeCooldownExpiry(strategy.PositionSettings.Cooldown);

            var newState = await stateRepository.UpdateStateOnClose(
                strategy.Id,
                trade.Ticker,
                closeValue,
                entryCost,
                cooldownExpiry);

            if (newState == null)
            {
                logger.LogWarning(
                    "State update skipped on close for strategy {StrategyId}, ticker {Ticker}. " +
                    "State shows no open positions; reconciliation will correct it.",
                    strategy.Id, trade.Ticker);
                return;
            }

            logger.LogInformation(
                "Updated state on close for strategy {StrategyId}, ticker {Ticker}, closeValue {CloseValue}, profit {Profit}, cooldown until {CooldownExpiry}",
                strategy.Id, trade.Ticker, closeValue, sellResult.Profit, cooldownExpiry);

            await WriteBalanceHistoryRecord(newState);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error updating state on close for strategy {StrategyId}, ticker {Ticker}",
                strategy.Id, trade.Ticker);
        }
    }

    /// <summary>
    /// Writes a balance history snapshot from the post-close state returned by the atomic update.
    /// </summary>
    private async Task WriteBalanceHistoryRecord(StrategyStateRecord state)
    {
        try
        {
            var history = new BalanceHistoryRecord
            {
                StrategyId = state.StrategyId,
                Date = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"),
                CashBalance = state.CashBalance,
                TotalEntryCost = state.TotalEntryCost,
                UnrealizedPnl = state.UnrealizedPnl, // Refreshed separately by the P/L job
                PositionValue = state.PositionValue,
                CurrentBalance = state.CurrentBalance,
                OpenPositionsCount = state.OpenPositionsCount,
                RecordedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                SnapshotType = "close"
            };

            var success = await stateRepository.WriteBalanceHistoryRecord(history);

            if (success)
            {
                logger.LogDebug(
                    "Wrote balance history for strategy {StrategyId}, date {Date}, balance {Balance}",
                    state.StrategyId, history.Date, history.CurrentBalance);
            }
            else
            {
                logger.LogWarning(
                    "Failed to write balance history for strategy {StrategyId} on {Date}",
                    state.StrategyId, history.Date);
            }
        }
        catch (Exception ex)
        {
            // Balance history write failure is non-critical
            logger.LogError(ex,
                "Error writing balance history for strategy {StrategyId}",
                state.StrategyId);
        }
    }
}
