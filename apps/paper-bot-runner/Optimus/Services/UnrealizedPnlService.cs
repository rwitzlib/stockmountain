using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Records;
using MarketViewer.Contracts.Records.Strategy;
using Massive.Client.Interfaces;
using Optimus.Infrastructure.Repositories;

namespace Optimus.Services;

/// <summary>
/// Service for calculating and updating unrealized P/L for strategy states.
/// Can be called on-demand (API) or from a scheduled job.
/// </summary>
public class UnrealizedPnlService(
    StrategyStateRepository stateRepository,
    TradeRepository tradeRepository,
    IMassiveClient massiveClient,
    ILogger<UnrealizedPnlService> logger)
{
    private const int SnapshotBatchSize = 500;

    /// <summary>
    /// Refreshes the unrealized P/L for a single strategy.
    /// Fetches current prices for all open positions and updates the state.
    /// </summary>
    /// <param name="strategyId">The strategy ID to refresh</param>
    /// <param name="startingBalance">Starting balance for lazy state initialization</param>
    /// <returns>The updated state, or null if refresh failed</returns>
    public async Task<StrategyStateRecord> RefreshUnrealizedPnl(string strategyId, decimal startingBalance)
    {
        try
        {
            // 1. Get current state
            var state = await stateRepository.GetOrCreateState(strategyId, startingBalance);
            if (state == null)
            {
                logger.LogError("Failed to get state for strategy {StrategyId}", strategyId);
                return null;
            }

            // 2. If no open positions, unrealized P/L is 0
            // Note: We only check OpenPositionsCount, NOT OpenTickers.Count, because:
            // - When AllowSimultaneous=true, multiple positions in the same ticker share one entry in OpenTickers
            // - Closing one position removes the ticker even if other positions in that ticker remain
            // - OpenPositionsCount is the authoritative count of open positions
            if (state.OpenPositionsCount == 0)
            {
                if (state.UnrealizedPnl != 0)
                {
                    // Update to 0 if it was non-zero
                    await stateRepository.UpdateUnrealizedPnl(strategyId, 0, state.Version);
                    state.UnrealizedPnl = 0;
                }
                return state;
            }

            // 3. Fetch open trades for this strategy
            var openTrades = await tradeRepository.ListTradesByStrategy(strategyId, null, TradeStatus.Open);
            var tradeList = openTrades.ToList();

            if (tradeList.Count == 0)
            {
                logger.LogWarning(
                    "State shows {Count} open positions but no open trades found for strategy {StrategyId}",
                    state.OpenPositionsCount, strategyId);
                return state;
            }

            // 4. Fetch current prices for all unique tickers in batched snapshot calls
            var uniqueTickers = tradeList.Select(t => t.Ticker).Distinct().ToList();
            var priceMap = await GetCurrentPrices(uniqueTickers);

            // 5. Calculate unrealized P/L for each position
            decimal totalUnrealizedPnl = 0;
            int pricedPositionCount = 0;

            foreach (var trade in tradeList)
            {
                if (priceMap.TryGetValue(trade.Ticker, out var currentPrice))
                {
                    var currentValue = currentPrice * trade.Shares;
                    var unrealizedPnl = currentValue - (decimal)trade.EntryPosition;
                    totalUnrealizedPnl += unrealizedPnl;
                    pricedPositionCount++;

                    logger.LogDebug(
                        "Ticker {Ticker}: Entry={Entry:C}, Current={Current:C}, Shares={Shares}, UnrealizedPnL={Pnl:C}",
                        trade.Ticker, trade.EntryPosition, currentValue, trade.Shares, unrealizedPnl);
                }
                else
                {
                    logger.LogWarning("Could not get price for ticker {Ticker}", trade.Ticker);
                }
            }

            // 6. Only update state if we successfully priced at least one position
            // This prevents resetting P/L to 0 when price fetches fail
            if (pricedPositionCount == 0)
            {
                logger.LogWarning(
                    "Could not get prices for any positions in strategy {StrategyId}. Skipping P/L update to preserve existing value.",
                    strategyId);
                return state;
            }

            if (pricedPositionCount < tradeList.Count)
            {
                logger.LogWarning(
                    "Only got prices for {PricedCount}/{TotalCount} positions in strategy {StrategyId}. P/L may be incomplete.",
                    pricedPositionCount, tradeList.Count, strategyId);
            }

            var updated = await stateRepository.UpdateUnrealizedPnl(strategyId, totalUnrealizedPnl, state.Version);

            if (updated)
            {
                logger.LogInformation(
                    "Updated unrealized P/L for strategy {StrategyId}: {Pnl:C} ({PositionCount} positions)",
                    strategyId, totalUnrealizedPnl, pricedPositionCount);
                state.UnrealizedPnl = totalUnrealizedPnl;
            }
            else
            {
                logger.LogWarning(
                    "Failed to update unrealized P/L for strategy {StrategyId} - version mismatch",
                    strategyId);
            }

            return state;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error refreshing unrealized P/L for strategy {StrategyId}", strategyId);
            return null;
        }
    }

    /// <summary>
    /// Calculates unrealized P/L without updating state.
    /// Useful for on-demand dashboard queries.
    /// </summary>
    public async Task<UnrealizedPnlResult> CalculateUnrealizedPnl(string strategyId)
    {
        try
        {
            var openTrades = await tradeRepository.ListTradesByStrategy(strategyId, null, TradeStatus.Open);
            var tradeList = openTrades.ToList();

            if (tradeList.Count == 0)
            {
                return new UnrealizedPnlResult
                {
                    StrategyId = strategyId,
                    TotalUnrealizedPnl = 0,
                    PositionCount = 0,
                    Positions = []
                };
            }

            // Fetch current prices for all unique tickers in batched snapshot calls
            var uniqueTickers = tradeList.Select(t => t.Ticker).Distinct().ToList();
            var priceMap = await GetCurrentPrices(uniqueTickers);

            var positions = new List<PositionPnlResult>();
            decimal totalPnl = 0;

            foreach (var trade in tradeList)
            {
                if (priceMap.TryGetValue(trade.Ticker, out var currentPrice))
                {
                    var currentValue = currentPrice * trade.Shares;
                    var unrealizedPnl = currentValue - (decimal)trade.EntryPosition;
                    totalPnl += unrealizedPnl;

                    positions.Add(new PositionPnlResult
                    {
                        Ticker = trade.Ticker,
                        Shares = trade.Shares,
                        EntryPrice = (decimal)trade.EntryPrice,
                        CurrentPrice = currentPrice,
                        EntryValue = (decimal)trade.EntryPosition,
                        CurrentValue = currentValue,
                        UnrealizedPnl = unrealizedPnl,
                        UnrealizedPnlPercent = trade.EntryPosition > 0 
                            ? (float)(unrealizedPnl / (decimal)trade.EntryPosition * 100)
                            : 0
                    });
                }
            }

            return new UnrealizedPnlResult
            {
                StrategyId = strategyId,
                TotalUnrealizedPnl = totalPnl,
                PositionCount = positions.Count,
                Positions = positions
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calculating unrealized P/L for strategy {StrategyId}", strategyId);
            return null;
        }
    }

    /// <summary>
    /// Gets current prices for multiple tickers in batched Massive snapshot calls.
    /// Tickers missing from the result (e.g. halted) are simply absent from the map.
    /// </summary>
    private async Task<Dictionary<string, decimal>> GetCurrentPrices(IEnumerable<string> tickers)
    {
        var prices = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var chunk in tickers.Chunk(SnapshotBatchSize))
        {
            var response = await massiveClient.GetAllTickersSnapshot(string.Join(',', chunk));

            foreach (var snapshot in response?.Tickers ?? [])
            {
                if (snapshot.Ticker is not null && snapshot.Minute?.Close > 0)
                {
                    prices[snapshot.Ticker] = (decimal)snapshot.Minute.Close;
                }
            }
        }

        return prices;
    }
}

/// <summary>
/// Result of unrealized P/L calculation.
/// </summary>
public class UnrealizedPnlResult
{
    public string StrategyId { get; set; }
    public decimal TotalUnrealizedPnl { get; set; }
    public int PositionCount { get; set; }
    public List<PositionPnlResult> Positions { get; set; } = [];
}

/// <summary>
/// P/L details for a single position.
/// </summary>
public class PositionPnlResult
{
    public string Ticker { get; set; }
    public int Shares { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal EntryValue { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal UnrealizedPnl { get; set; }
    public float UnrealizedPnlPercent { get; set; }
}

