using MarketViewer.Contracts.Models.Indicator;
using MarketViewer.Contracts.Responses.Market;

namespace MarketViewer.Studies.Studies;

public class RSI : IStudy
{
    private static string[] ValidTypes { get; set; } = ["sma", "ema", "wilders"];

    public List<IndicatorPoint> Compute(string[] parameters, ref StocksResponse stocksResponse)
    {
        var series = new List<IndicatorPoint>();

        if (!Validate(parameters, stocksResponse,
            out var weight,
            out var overBoughtLevel,
            out var overSoldLevel,
            out var type))
        {
            return [];
        }

        List<float> upMoves = [];
        List<float> downMoves = [];
        List<float> avgUps = [];
        List<float> avgDowns = [];
        for (int i = 1; i < stocksResponse.Results.Count; i++)
        {
            var value = stocksResponse.Results[i].Close - stocksResponse.Results[i - 1].Close;
            if (value > 0)
            {
                upMoves.Add(value);
                downMoves.Add(0);
            }
            else if (value == 0)
            {
                upMoves.Add(0);
                downMoves.Add(0);
            }
            else
            {
                upMoves.Add(0);
                downMoves.Add(Math.Abs(value));
            }

            if (upMoves.Count < weight || downMoves.Count < weight)
            {
                continue;
            }

            var (avgUp, avgDown) = type.ToLowerInvariant() switch
            {
                "sma" => GetSimpleMovingAverageRSI(upMoves, downMoves, i, weight),
                "ema" => GetExponentialMovingAverageRSI(series, upMoves, downMoves, avgUps, avgDowns, i, weight),
                "wilders" => GetWildersMovingAverageRSI(series, upMoves, downMoves, avgUps, avgDowns, i, weight),
                _ => throw new NotImplementedException()
            };
            avgUps.Add(avgUp);
            avgDowns.Add(avgDown);

            var rsi = 100 - 100 / (1 + avgUp / avgDown);

            series.Add(new RsiPoint
            {
                Value = rsi,
                Upper = overBoughtLevel,
                Lower = overSoldLevel,
                Timestamp = stocksResponse.Results[i].Timestamp
            });
        }

        return series;
    }

    private static bool Validate(
        IReadOnlyList<object> parameters,
        StocksResponse stocksResponse,
        out int weight,
        out int overboughtLevel,
        out int oversoldLevel,
        out string type)
    {
        weight = 0;
        overboughtLevel = 0;
        oversoldLevel = 0;
        type = string.Empty;

        if (parameters is null || parameters.Count != 4)
        {
            return false;
        }

        if (int.TryParse(parameters[0].ToString(), out var _weight)
            && int.TryParse(parameters[1].ToString(), out var _overboughtLevel)
            && int.TryParse(parameters[2].ToString(), out var _oversoldLevel)
            && ValidTypes.Contains(parameters[3].ToString().ToLowerInvariant()))
        {
            weight = _weight;
            overboughtLevel = _overboughtLevel;
            oversoldLevel = _oversoldLevel;
            type = parameters[3].ToString().ToLowerInvariant();
        }
        else
        {
            return false;
        }

        if (weight < 1 || overboughtLevel < 1 || oversoldLevel < 1)
        {
            return false;
        }

        if (stocksResponse.Results is null || stocksResponse.Results.Count < weight)
        {
            return false;
        }

        return true;
    }

    private static (float, float) GetSimpleMovingAverageRSI(List<float> upMoves, List<float> downMoves, int index, int weight)
    {
        var avgUp = upMoves.GetRange(index - weight, weight).Sum() / weight;
        var avgDown = downMoves.GetRange(index - weight, weight).Sum() / weight;

        return (avgUp, avgDown);
    }

    private static (float, float) GetExponentialMovingAverageRSI(
        List<IndicatorPoint> series,
        List<float> upMoves,
        List<float> downMoves,
        List<float> avgUps,
        List<float> avgDowns,
        int index,
        int weight)
    {
        if (series.Count == 0)
        {
            return GetSimpleMovingAverageRSI(upMoves, downMoves, index, weight);
        }

        var a = 2f / (weight + 1);

        var avgUp = a * upMoves.Last() + (1 - a) * avgUps.Last();
        var avgDown = a * downMoves.Last() + (1 - a) * avgDowns.Last();

        return (avgUp, avgDown);
    }

    private static (float, float) GetWildersMovingAverageRSI(List<IndicatorPoint> series,
        List<float> upMoves,
        List<float> downMoves,
        List<float> avgUps,
        List<float> avgDowns,
        int index,
        int weight)
    {
        if (series.Count == 0)
        {
            return GetSimpleMovingAverageRSI(upMoves, downMoves, index, weight);
        }

        var a = 1f / weight;

        var avgUp = a * upMoves.Last() + (1 - a) * avgUps.Last();
        var avgDown = a * downMoves.Last() + (1 - a) * avgDowns.Last();

        return (avgUp, avgDown);
    }
}