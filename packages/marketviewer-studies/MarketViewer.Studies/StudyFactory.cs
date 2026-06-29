using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models.Indicator;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Studies.Studies;

namespace MarketViewer.Studies;

public class StudyFactory(
    SMA sma,
    EMA ema,
    MACD macd,
    RSI rsi,
    VWAP vwap,
    RVOL rvol,
    MAMR mamr)
{
    public IndicatorResponse Compute(Indicator indicator, StocksResponse stocksResponse)
    {
        var results = indicator.Type switch
        {
            StudyType.sma => sma.Compute(indicator.Parameters, ref stocksResponse),
            StudyType.ema => ema.Compute(indicator.Parameters, ref stocksResponse),
            StudyType.vwap => vwap.Compute(indicator.Parameters, ref stocksResponse),
            StudyType.macd => macd.Compute(indicator.Parameters, ref stocksResponse),
            StudyType.rsi => rsi.Compute(indicator.Parameters, ref stocksResponse),
            StudyType.rvol => rvol.Compute(indicator.Parameters, ref stocksResponse),
            StudyType.mamr => mamr.Compute(indicator.Parameters, ref stocksResponse),
            _ => []
        };

        if (results.Count == 0)
        {
            return null;
        }

        var response = new IndicatorResponse
        {
            Name = BuildIndicatorName(indicator),
            Results = results
        };

        return response;
    }

    private static string BuildIndicatorName(Indicator indicatorParameters)
    {
        var name = $"{indicatorParameters.Type}";

        if (indicatorParameters.Parameters is not null && indicatorParameters.Parameters.Any())
        {
            name += $"({string.Join(',', indicatorParameters.Parameters)})";
        }

        if (!string.IsNullOrEmpty(indicatorParameters.Selector))
        {
            name += $".{indicatorParameters.Selector}";
        }
        return name;
    }
}