using MarketViewer.Contracts.Models.Indicator;
using MarketViewer.Contracts.Responses.Market;

namespace MarketViewer.Studies.Studies;

public class VWAP : IStudy
{
    public List<IndicatorPoint> Compute(string[] parameters, ref StocksResponse stocksResponse)
    {
        var series = new List<IndicatorPoint>();

        if (!Validate(parameters, stocksResponse))
        {
            return [];
        }

        float cumulativeVolume = 0;
        float cumulativeDollarVolume = 0;
        var currentDay = DateTimeOffset.FromUnixTimeMilliseconds(stocksResponse.Results.First().Timestamp).Day;

        foreach (var candle in stocksResponse.Results)
        {
            // Reset VWAP calculation at beginning of each day
            if (DateTimeOffset.FromUnixTimeMilliseconds(candle.Timestamp).Day > currentDay)
            {
                cumulativeVolume = 0;
                cumulativeDollarVolume = 0;
                currentDay = DateTimeOffset.FromUnixTimeMilliseconds(candle.Timestamp).Day;
            }

            cumulativeVolume += candle.Volume;
            cumulativeDollarVolume += candle.Vwap * candle.Volume;

            var vwap = cumulativeDollarVolume / cumulativeVolume;

            series.Add(new IndicatorPoint
            {
                Timestamp = candle.Timestamp,
                Value = vwap
            });
        }

        return series;
    }

    private static bool Validate(IReadOnlyList<object> parameters, StocksResponse stocksResponse)
    {
        if (parameters is not null)
        {
            return false;
        }

        if (stocksResponse.Results is null || !stocksResponse.Results.Any())
        {
            return false;
        }

        return true;
    }
}