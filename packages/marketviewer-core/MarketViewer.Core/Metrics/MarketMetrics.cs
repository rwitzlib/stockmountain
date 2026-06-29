using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models.Indicator;
using System.Diagnostics.Metrics;

namespace MarketViewer.Core.Metrics;

public class MarketMetrics
{
    private readonly Counter<int> _tickerCounter;
    private readonly Counter<int> _timeframeCounter;
    private readonly Counter<int> _indicatorCounter;

    public MarketMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("MarketViewer.Market");
        _tickerCounter = meter.CreateCounter<int>("marketviewer.stocks.ticker");
        _timeframeCounter = meter.CreateCounter<int>("marketviewer.stocks.timeframe");
        _indicatorCounter = meter.CreateCounter<int>("marketviewer.stocks.indicator");
    }

    public void IncrementTickerCount(string ticker)
    {
        _tickerCounter.Add(1, new KeyValuePair<string, object>("marketviewer.stocks.ticker", ticker));
    }

    public void IncrementTimeframe(int multiplier, Timespan timespan)
    {
        _timeframeCounter.Add(1, new KeyValuePair<string, object>("marketviewer.stocks.timeframe", $"{multiplier}{timespan.ToString().ToCharArray()[0]}"));
    }

    public void IncrementIndicator(Indicator indicator)
    {
        var indicatorKey = indicator.ToString();
        _indicatorCounter.Add(1, new KeyValuePair<string, object>("marketviewer.stocks.indicator", indicatorKey));
    }
}
