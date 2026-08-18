using MarketViewer.Filters.Registry;
using MarketViewer.Filters.Interfaces;
using Massive.Client.Models;

namespace MarketViewer.Filters.Functions.Indicators;

/// <summary>
/// Volume-weighted average price, anchored per session.
///
/// <code>vwap()</code> / <code>vwap(session)</code> — resets at 09:30 America/New_York and runs
/// through after-hours AND the next pre-market until the following 09:30 reset (over a weekend or
/// holiday it simply keeps running). So during the regular session it is the "VWAP" a broker chart
/// shows; pre-market it is the prior session's VWAP carried forward, so a pre-market
/// <c>close &gt; vwap()</c> means "above yesterday's session VWAP". Bars before the very first
/// 09:30 in the data have no value (ordinary warm-up).
///
/// <code>vwap(day)</code> — resets when the Eastern calendar date changes, so pre-market bars
/// are included (extended-hours VWAP).
///
/// Per bar the price used is Massive's own bar VWAP (<c>vw</c>) weighted by volume, which is the
/// exact session VWAP; when a bar has no <c>vw</c> (e.g. the backtester's forming candle) the
/// typical price (h+l+c)/3 is used. A bar STARTS a date's session when its span
/// (start + timeframe) ends after 09:30 ET on that date and no bar of that date has yet — so the
/// 09:00 hourly bar and the daily bar (which starts at midnight) open the session, the 09:29
/// minute bar still belongs to the previous one. On daily bars every bar is its own session and
/// <c>vwap()</c> equals the bar's <c>vw</c>.
///
/// Reference implementation: tools/golden/compute_reference.py (`vwap()`, `vwap(day)`).
/// </summary>
[FilterFunction("vwap", Kind = FunctionKind.Series,
    Signature = "vwap([anchor])", Snippet = "vwap()",
    Description = "Session VWAP anchored at 09:30 ET (no value pre-market); vwap(day) anchors at the Eastern date change to include pre-market",
    Params = ["anchor?"], Cost = 1.5, Selectivity = 0.5, Contexts = FilterContext.All)]
public class VwapFunction : ISeriesFunction, IIncrementalSeriesFunction
{
    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    private static readonly TimeOnly SessionOpen = new(9, 30);
    private static readonly string[] ValidAnchors = ["session", "day"];

    public string Name => "vwap";

    public object Execute(object[] parameters, ExpressionContext context)
    {
        var anchor = ParseAnchor(parameters);
        var data = context.StockData.Results;
        var series = new List<IIndicatorResult>(data.Count);

        int currentKey = -1;
        double cumPv = 0, cumV = 0;

        for (int i = 0; i < data.Count; i++)
        {
            var opens = SessionOpenedBy(data[i].Timestamp, anchor, context.Timeframe);
            if (opens >= 0 && opens != currentKey)
            {
                currentKey = opens; // this bar opens a new session: reset
                cumPv = 0;
                cumV = 0;
            }
            if (currentKey < 0)
                continue; // no session opened yet (history starts pre-market)

            series.Add(MakePoint(data[i], i, currentKey, cumPv, cumV, out cumPv, out cumV));
        }

        return series;
    }

    public object Append(object[] parameters, ExpressionContext context, object previousResult)
    {
        var anchor = ParseAnchor(parameters);
        var data = context.StockData.Results;
        var prev = previousResult as List<IIndicatorResult> ?? new List<IIndicatorResult>();

        if (prev.Count == 0 || prev[^1] is not VwapResult last || last.BarIndex >= data.Count)
            return Execute(parameters, context);

        var result = new List<IIndicatorResult>(prev.Count + Math.Max(0, data.Count - last.BarIndex));
        result.AddRange(prev);

        int currentKey = last.AnchorKey;
        double cumPv = last.CumulativePriceVolume, cumV = last.CumulativeVolume;
        int startIndex = last.BarIndex + 1;

        // The last bar we priced may have been mutated in place (a forming candle): re-price it
        // from the sums that preceded it. Timestamp equality identifies "same bar".
        if (data[last.BarIndex].Timestamp == last.Timestamp)
        {
            result[^1] = MakePoint(data[last.BarIndex], last.BarIndex, last.AnchorKey, last.PriorPriceVolume, last.PriorVolume, out cumPv, out cumV);
        }
        else
        {
            return Execute(parameters, context); // data was replaced under us; rebuild
        }

        for (int i = startIndex; i < data.Count; i++)
        {
            var opens = SessionOpenedBy(data[i].Timestamp, anchor, context.Timeframe);
            if (opens >= 0 && opens != currentKey)
            {
                currentKey = opens;
                cumPv = 0;
                cumV = 0;
            }

            result.Add(MakePoint(data[i], i, currentKey, cumPv, cumV, out cumPv, out cumV));
        }

        return result;
    }

    // ---- helpers

    private static string ParseAnchor(object[] parameters)
    {
        if (parameters.Length > 1)
            throw new ArgumentException("vwap() takes at most 1 parameter: vwap() or vwap(session|day)");

        var anchor = parameters.Length == 0 ? "session" : parameters[0]?.ToString()?.ToLowerInvariant() ?? "";
        if (!ValidAnchors.Contains(anchor))
            throw new ArgumentException($"Invalid vwap anchor: {anchor}. Valid anchors: {string.Join(", ", ValidAnchors)}");
        return anchor;
    }

    /// <summary>
    /// The session a bar OPENS, as the Eastern date's DayNumber, or -1 if the bar does not open one.
    /// "day": every bar opens (or continues) its Eastern date. "session": a bar opens its date's
    /// session when its span [start, start + timeframe) ends after 09:30 ET; earlier bars (pre-market)
    /// continue whatever session is running.
    /// </summary>
    public static int SessionOpenedBy(long timestamp, string anchor, Contracts.Models.Timeframe timeframe)
    {
        var start = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(timestamp), Eastern).DateTime;
        var date = DateOnly.FromDateTime(start);
        if (anchor == "day")
            return date.DayNumber;

        var end = timeframe.Timespan switch
        {
            Contracts.Enums.Timespan.minute => start.AddMinutes(timeframe.Multiplier),
            Contracts.Enums.Timespan.hour => start.AddHours(timeframe.Multiplier),
            _ => start.AddDays(timeframe.Multiplier), // day and larger: the bar always spans the open
        };
        var open = start.Date.Add(SessionOpen.ToTimeSpan());
        return end > open ? date.DayNumber : -1;
    }

    private static VwapResult MakePoint(Bar bar, int index, int key, double priorPv, double priorV, out double cumPv, out double cumV)
    {
        double price = bar.Vwap > 0 ? bar.Vwap : (bar.High + bar.Low + bar.Close) / 3.0;
        double volume = Math.Max(0, bar.Volume);
        cumPv = priorPv + price * volume;
        cumV = priorV + volume;

        return new VwapResult
        {
            Timestamp = bar.Timestamp,
            Value = cumV > 0 ? cumPv / cumV : price,
            BarIndex = index,
            AnchorKey = key,
            PriorPriceVolume = priorPv,
            PriorVolume = priorV,
            CumulativePriceVolume = cumPv,
            CumulativeVolume = cumV
        };
    }
}
