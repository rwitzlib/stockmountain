using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models.Strategy;
using Optimus.Adapter.Config;

namespace Optimus.Adapter;

/// <summary>
/// Pure sizing and pricing math for Alpaca orders, extracted for unit testing.
/// </summary>
public static class AlpacaOrderMath
{
    /// <summary>Whole shares affordable at the given price; 0 when the price is invalid.</summary>
    public static int ComputeShares(float positionSize, float price)
    {
        return price > 0 ? (int)(positionSize / price) : 0;
    }

    /// <summary>
    /// Stop price for the GTC stop-market disaster backstop: entry minus the configured
    /// multiple of the strategy's logical stop distance. A percent stop reads as percent
    /// of entry (position value scales linearly with price); a flat stop is dollars on the
    /// whole position, so its per-share distance divides by the share count. Strategies
    /// without a stop loss fall back to a config default — a broker-side position never
    /// goes unprotected. Returns null only for invalid inputs.
    /// </summary>
    public static decimal? ComputeBackstopStopPrice(float entryPrice, int shares, Exit stopLoss, AlpacaAdapterConfig config)
    {
        if (entryPrice <= 0 || shares <= 0)
        {
            return null;
        }

        var entry = (decimal)entryPrice;

        var distancePerShare = stopLoss is { Value: not 0 }
            ? stopLoss.Type switch
            {
                ExitValueType.percent => entry * (decimal)Math.Abs(stopLoss.Value) / 100m,
                ExitValueType.flat => (decimal)Math.Abs(stopLoss.Value) / shares,
                _ => entry * (decimal)config.FallbackBackstopPercent / 100m
            }
            : entry * (decimal)config.FallbackBackstopPercent / 100m;

        var stopPrice = entry - (decimal)config.BackstopStopMultiplier * distancePerShare;

        return RoundStopPrice(stopPrice);
    }

    /// <summary>
    /// Alpaca accepts at most 2 decimals on stop prices at or above $1 and 4 decimals
    /// below $1. Anything at or below zero clamps to the minimum tick.
    /// </summary>
    public static decimal RoundStopPrice(decimal price)
    {
        if (price <= 0.0001m)
        {
            return 0.0001m;
        }

        return price >= 1m
            ? Math.Round(price, 2, MidpointRounding.ToZero)
            : Math.Round(price, 4, MidpointRounding.ToZero);
    }
}
