using System;
using System.Collections.Generic;

namespace Massive.Client.Models;

/// <summary>
/// Volume-weighted VWAP arithmetic for candles built or extended from smaller bars (a forming
/// 5m/1h/1d candle fed minute by minute, or an hour/day bar aggregated from a day's minutes).
/// Massive's <c>vw</c> on a completed candle is Σ(price·volume)/Σvolume over its trades; the same
/// candle rebuilt from minute bars is Σ(vw_i·v_i)/Σv_i, which is exact given the minutes' <c>vw</c>
/// (the earlier (c+h+l)/3 "typical price" approximation was plan-14 follow-up 7). Every consumer
/// of forming candles — the backtester's <c>UpdateLatestCandle</c>/<c>RebuildOverlappingCandle</c>
/// and the live API's cache merges/aggregation — must use this so <c>vwap()</c> reads the same
/// numbers live and in backtests.
///
/// Sums are carried in double; only the final result is narrowed to the bar's float. Merging is
/// exact as a recurrence — a candle's VWAP × its volume is its price·volume sum — so no extra state
/// is needed on <see cref="Bar"/>.
/// </summary>
public static class BarVwap
{
    /// <summary>(close+high+low)/3 — the fallback when a bar carries no volume/VWAP.</summary>
    public static float TypicalPrice(Bar bar) => (bar.Close + bar.High + bar.Low) / 3f;

    /// <summary>
    /// VWAP of a candle after <paramref name="incoming"/> is folded into <paramref name="current"/>,
    /// where <paramref name="current"/> still holds the pre-merge volume and VWAP. A zero-volume
    /// incoming bar leaves the VWAP unchanged; if neither side has volume the result is the merged
    /// candle's typical price computed from <paramref name="mergedTypicalPrice"/>.
    /// </summary>
    public static float Merge(Bar current, Bar incoming, float mergedTypicalPrice)
    {
        double currentVolume = Math.Max(0, current.Volume);
        double incomingVolume = Math.Max(0, incoming.Volume);
        double totalVolume = currentVolume + incomingVolume;
        if (totalVolume <= 0)
        {
            return mergedTypicalPrice;
        }

        double currentPrice = current.Vwap > 0 ? current.Vwap : TypicalPrice(current);
        double incomingPrice = incoming.Vwap > 0 ? incoming.Vwap : TypicalPrice(incoming);
        return (float)((currentPrice * currentVolume + incomingPrice * incomingVolume) / totalVolume);
    }

    /// <summary>
    /// VWAP of a candle aggregated from <paramref name="bars"/>: Σ(vw·v)/Σv. Bars without volume do
    /// not contribute; with no volume at all the result is <paramref name="fallbackTypicalPrice"/>.
    /// </summary>
    public static float Aggregate(IEnumerable<Bar> bars, float fallbackTypicalPrice)
    {
        double priceVolume = 0, volume = 0;
        foreach (var bar in bars)
        {
            double v = Math.Max(0, bar.Volume);
            if (v <= 0)
            {
                continue;
            }

            double price = bar.Vwap > 0 ? bar.Vwap : TypicalPrice(bar);
            priceVolume += price * v;
            volume += v;
        }

        return volume > 0 ? (float)(priceVolume / volume) : fallbackTypicalPrice;
    }
}
