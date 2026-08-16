using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using Massive.Client.Models;

namespace Backtest.Lambda.Utilities;

public static class StocksResponseExtensions
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="stocksResponse"></param>
    /// <param name="timeframe"></param>
    /// <param name="nextCandle"></param>
    /// <exception cref="NotSupportedException"></exception>
    public static void UpdateLatestCandle(this StocksResponse stocksResponse, Timeframe timeframe, Bar nextCandle)
    {
        if (timeframe.Multiplier == 1 && timeframe.Timespan == Timespan.minute)
        {
            stocksResponse.Results.Add(nextCandle);
            return;
        }

        var lastCandle = stocksResponse.Results.Last();
        var lastCandleStart = DateTimeOffset.FromUnixTimeMilliseconds(lastCandle.Timestamp);

        var lastCandleEnd = timeframe.Timespan switch
        {
            Timespan.minute => lastCandleStart.AddMinutes(timeframe.Multiplier),
            Timespan.hour => lastCandleStart.AddHours(timeframe.Multiplier),
            Timespan.day => lastCandleStart.AddDays(timeframe.Multiplier),
            _ => throw new NotSupportedException($"Timespan {timeframe.Timespan} is not supported."),
        };

        if (nextCandle.Timestamp >= lastCandleEnd.ToUnixTimeMilliseconds())
        {
            // New candle. Anchor it to the timeframe grid implied by the previous candle rather
            // than to the arriving minute: if minutes are missing (illiquid names, after-hours),
            // "16:41" must still open the 16:40 candle or every later candle drifts off the
            // boundaries Massive uses for the same timeframe.
            var candleStart = lastCandleEnd;
            while (true)
            {
                var candleEnd = timeframe.Timespan switch
                {
                    Timespan.minute => candleStart.AddMinutes(timeframe.Multiplier),
                    Timespan.hour => candleStart.AddHours(timeframe.Multiplier),
                    Timespan.day => candleStart.AddDays(timeframe.Multiplier),
                    _ => throw new NotSupportedException($"Timespan {timeframe.Timespan} is not supported."),
                };

                if (nextCandle.Timestamp < candleEnd.ToUnixTimeMilliseconds())
                {
                    break;
                }

                candleStart = candleEnd;
            }

            var newCandle = nextCandle.Clone();
            newCandle.Timestamp = candleStart.ToUnixTimeMilliseconds();
            stocksResponse.Results.Add(newCandle);
            return;
        }

        // Update existing candle
        if (nextCandle.High > lastCandle.High)
        {
            lastCandle.High = nextCandle.High;
        }

        if (nextCandle.Low < lastCandle.Low)
        {
            lastCandle.Low = nextCandle.Low;
        }

        lastCandle.Close = nextCandle.Close;

        // TODO: How to do a more precise VWAP?
        lastCandle.Vwap = (lastCandle.Close + lastCandle.High + lastCandle.Low) / 3;

        lastCandle.Volume += nextCandle.Volume;
        lastCandle.TransactionCount += nextCandle.TransactionCount;
    }
}
