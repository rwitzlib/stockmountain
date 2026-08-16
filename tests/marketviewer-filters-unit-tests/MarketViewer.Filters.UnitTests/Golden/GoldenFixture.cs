using System.Text.Json;
using System.Text.Json.Serialization;
using MarketViewer.Contracts.Responses.Market;

namespace MarketViewer.Filters.UnitTests.Golden;

/// <summary>
/// A golden fixture: real Massive bars (TestData/Golden/bars/*.json, verbatim aggregates response)
/// plus independently computed reference indicator series (TestData/Golden/reference/*.indicators.json,
/// produced by tools/golden/compute_reference.py). See plans/14-golden-filter-tests.md.
/// </summary>
public sealed class GoldenFixture
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly Dictionary<string, GoldenFixture> Cache = new();
    private static readonly object CacheLock = new();

    public static string GoldenRoot => Path.Combine(AppContext.BaseDirectory, "TestData", "Golden");

    public string Name { get; }
    public StocksResponse Bars { get; }
    public ReferenceFile Reference { get; }

    private GoldenFixture(string name, StocksResponse bars, ReferenceFile reference)
    {
        Name = name;
        Bars = bars;
        Reference = reference;
    }

    /// <summary>Names of every fixture that has both a bars file and a reference file.</summary>
    public static IEnumerable<string> Names()
    {
        var barsDir = Path.Combine(GoldenRoot, "bars");
        if (!Directory.Exists(barsDir)) yield break;
        foreach (var file in Directory.EnumerateFiles(barsDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (File.Exists(ReferencePath(name))) yield return name;
        }
    }

    public static GoldenFixture Load(string name)
    {
        lock (CacheLock)
        {
            if (Cache.TryGetValue(name, out var cached)) return cached;

            var bars = JsonSerializer.Deserialize<StocksResponse>(File.ReadAllText(BarsPath(name)), JsonOptions)
                ?? throw new InvalidOperationException($"Could not deserialize bars for {name}");
            var reference = JsonSerializer.Deserialize<ReferenceFile>(File.ReadAllText(ReferencePath(name)), JsonOptions)
                ?? throw new InvalidOperationException($"Could not deserialize reference for {name}");

            if (reference.BarCount != bars.Results.Count)
            {
                throw new InvalidOperationException(
                    $"{name}: reference has {reference.BarCount} bars but fixture has {bars.Results.Count} — re-run tools/golden/compute_reference.py");
            }

            var fixture = new GoldenFixture(name, bars, reference);
            Cache[name] = fixture;
            return fixture;
        }
    }

    /// <summary>A fresh, independent copy of the bars (tests mutate the list for incremental runs).</summary>
    public StocksResponse CloneBars() => new()
    {
        Ticker = Bars.Ticker,
        Status = Bars.Status,
        Results = Bars.Results.Select(b => b.Clone()).ToList()
    };

    private static string BarsPath(string name) => Path.Combine(GoldenRoot, "bars", name + ".json");
    private static string ReferencePath(string name) => Path.Combine(GoldenRoot, "reference", name + ".indicators.json");

    public sealed class ReferenceFile
    {
        [JsonPropertyName("source")] public string Source { get; set; } = "";
        [JsonPropertyName("generatedBy")] public string GeneratedBy { get; set; } = "";
        [JsonPropertyName("barCount")] public int BarCount { get; set; }
        [JsonPropertyName("series")] public Dictionary<string, double?[]> Series { get; set; } = new();
    }
}
