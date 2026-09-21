using MarketViewer.Contracts.Enums;

namespace MarketViewer.Contracts.Models.Strategy;

/// <summary>
/// Pure trailing-stop arithmetic shared by the backtester and the live exit evaluator so
/// both price the same stop from the same high-water mark (ADR 0006).
/// </summary>
public static class TrailingStopMath
{
    /// <summary>
    /// The price the trailing stop rests at given the high-water mark, or null when the
    /// stop is not configured, not yet armed (high-water mark below the activation gain),
    /// or unpriceable (unknown value type, no shares for a flat trail).
    /// </summary>
    public static float? StopPrice(TrailingStop trailingStop, float entryPrice, int shares, float highWaterMark)
    {
        if (trailingStop is null || trailingStop.Value <= 0 || entryPrice <= 0)
        {
            return null;
        }

        var activation = Math.Abs(trailingStop.Activation ?? 0);

        switch (trailingStop.Type)
        {
            case ExitValueType.percent:
                if (highWaterMark < entryPrice * (1 + activation / 100))
                {
                    return null;
                }

                return highWaterMark * (1 - Math.Abs(trailingStop.Value) / 100);

            case ExitValueType.flat:
                if (shares <= 0)
                {
                    return null;
                }

                // Flat values are position dollars, same convention as the fixed Exit.
                if (highWaterMark < entryPrice + activation / shares)
                {
                    return null;
                }

                return highWaterMark - Math.Abs(trailingStop.Value) / shares;

            default:
                return null;
        }
    }
}
