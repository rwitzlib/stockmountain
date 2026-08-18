using System.Text.Json;
using System.Text.Json.Serialization;
using MarketViewer.Contracts.Responses.Market;
using Massive.Client.Models;

namespace Backtest.Lambda.UnitTests.Golden;

/// <summary>
/// Access to the golden fixtures owned by the filters test project (linked into this project's
/// output via the csproj). See plans/14-golden-filter-tests.md, layer 3.
/// </summary>
internal static class GoldenData
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    public static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    private static readonly Dictionary<string, StocksResponse> BarsCache = new();
    private static readonly Dictionary<string, ReferenceFile> RefCache = new();

    private static string Root => Path.Combine(AppContext.BaseDirectory, "TestData", "Golden");

    public static StocksResponse Bars(string name)
    {
        lock (BarsCache)
        {
            if (!BarsCache.TryGetValue(name, out var response))
            {
                response = JsonSerializer.Deserialize<StocksResponse>(File.ReadAllText(Path.Combine(Root, "bars", name + ".json")), JsonOptions)!;
                BarsCache[name] = response;
            }
            // callers mutate; hand out deep copies
            return new StocksResponse { Ticker = response.Ticker, Status = response.Status, Results = response.Results.Select(b => b.Clone()).ToList() };
        }
    }

    public static ReferenceFile Reference(string name)
    {
        lock (RefCache)
        {
            if (!RefCache.TryGetValue(name, out var reference))
            {
                reference = JsonSerializer.Deserialize<ReferenceFile>(File.ReadAllText(Path.Combine(Root, "reference", name + ".indicators.json")), JsonOptions)!;
                RefCache[name] = reference;
            }
            return reference;
        }
    }

    public static IEnumerable<string> MinuteFixtures() =>
        Directory.EnumerateFiles(Path.Combine(Root, "bars"), "*_1m_*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n)!;

    public static DateOnly EasternDate(long timestamp) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(timestamp), Eastern).DateTime);

    public static DateTimeOffset EasternTime(DateOnly date, int hour, int minute)
    {
        var local = date.ToDateTime(new TimeOnly(hour, minute));
        return new DateTimeOffset(local, Eastern.GetUtcOffset(local));
    }

    /// <summary>ET trading dates present in a fixture, ascending.</summary>
    public static List<DateOnly> Dates(StocksResponse bars) =>
        bars.Results.Select(b => EasternDate(b.Timestamp)).Distinct().OrderBy(d => d).ToList();

    /// <summary>
    /// Straightforward independent OHLCV(+VWAP) aggregation of a set of minute bars. VWAP is
    /// Σ(vw·v)/Σv in double — the definition the forming-candle code must reproduce (plan 14 #7).
    /// </summary>
    public static Bar Aggregate(long timestamp, IReadOnlyList<Bar> minutes)
    {
        double priceVolume = minutes.Sum(b => (double)b.Vwap * b.Volume);
        double volume = minutes.Sum(b => b.Volume);
        return new Bar
        {
            Timestamp = timestamp,
            Open = minutes[0].Open,
            Close = minutes[^1].Close,
            High = minutes.Max(b => b.High),
            Low = minutes.Min(b => b.Low),
            Volume = volume,
            Vwap = volume > 0 ? (float)(priceVolume / volume) : (minutes[^1].Close + minutes.Max(b => b.High) + minutes.Min(b => b.Low)) / 3f,
            TransactionCount = minutes.Sum(b => b.TransactionCount)
        };
    }

    public sealed class ReferenceFile
    {
        [JsonPropertyName("barCount")] public int BarCount { get; set; }
        [JsonPropertyName("series")] public Dictionary<string, double?[]> Series { get; set; } = new();
    }
}
