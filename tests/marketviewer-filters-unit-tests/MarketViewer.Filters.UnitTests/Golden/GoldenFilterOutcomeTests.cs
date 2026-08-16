using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace MarketViewer.Filters.UnitTests.Golden;

/// <summary>
/// Layer 2 of plans/14-golden-filter-tests.md: whole filter scripts replayed through
/// <c>FilterSession</c> the way the backtester does (see <see cref="GoldenReplay"/>), compared to
/// TestData/Golden/outcomes/filters.json produced by tools/golden/compute_outcomes.py.
///
/// - kind "reference": expected true-timestamps were computed independently from the reference
///   indicator series — a mismatch means the engine and the reference disagree on semantics.
/// - kind "snapshot": no independent reference exists; expected is blessed by running with
///   GOLDEN_UPDATE=1 (this rewrites filters.json in place) and reviewed like a code change.
/// - knownBug != null: the case is expected to FAIL today; <see cref="Known_Bug_Still_Reproduces"/>
///   asserts that so the annotation gets removed when the bug is fixed.
/// </summary>
public class GoldenFilterOutcomeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private static readonly Lazy<OutcomesFile> Outcomes = new(LoadOutcomes);
    private static readonly object BlessLock = new();

    public static IEnumerable<object[]> Cases() =>
        Outcomes.Value.Cases.Where(c => c.KnownBug is null)
            .SelectMany(c => c.Expected.Keys.Select(fixture => new object[] { c.Id, fixture }));

    public static IEnumerable<object[]> KnownBugCases() =>
        Outcomes.Value.Cases.Where(c => c.KnownBug is not null)
            .SelectMany(c => c.Expected.Keys.Select(fixture => new object[] { c.Id, fixture }));

    [Theory]
    [MemberData(nameof(Cases))]
    public void Filter_Outcomes_Match(string caseId, string fixtureName)
    {
        var (c, actual, expected) = Evaluate(caseId, fixtureName);
        AssertSetsEqual(c, fixtureName, expected, actual);
    }

    [Theory]
    [MemberData(nameof(KnownBugCases))]
    public void Known_Bug_Still_Reproduces(string caseId, string fixtureName)
    {
        var c = Outcomes.Value.Cases.Single(x => x.Id == caseId);
        bool stillBroken;
        try
        {
            var (_, actual, expected) = Evaluate(caseId, fixtureName);
            stillBroken = !expected.SetEquals(actual);
        }
        catch (Exception)
        {
            stillBroken = true; // e.g. parser throws on the grouped expression
        }

        Assert.True(stillBroken,
            $"'{c.Id}' ({c.Script}) now matches its expected outcome on {fixtureName}. " +
            $"The known bug appears fixed — remove known_bug=\"{c.KnownBug}\" from tools/golden/compute_outcomes.py and regenerate.");
    }

    private static (OutcomeCase Case, HashSet<long> Actual, HashSet<long> Expected) Evaluate(string caseId, string fixtureName)
    {
        var c = Outcomes.Value.Cases.Single(x => x.Id == caseId);
        var fixture = GoldenFixture.Load(fixtureName);

        var result = GoldenReplay.Run(fixture, c.Script);
        var actual = result.TrueAt.ToHashSet();

        Assert.True(c.EvaluatedCount[fixtureName] == result.EvaluatedCount,
            $"{c.Id}/{fixtureName}: replay evaluated {result.EvaluatedCount} bars, compute_outcomes.py evaluated {c.EvaluatedCount[fixtureName]} — replay windows out of sync");

        var expectedList = c.Expected[fixtureName];
        if (expectedList is null)
        {
            if (Environment.GetEnvironmentVariable("GOLDEN_UPDATE") == "1")
            {
                Bless(c.Id, fixtureName, result.TrueAt);
                expectedList = result.TrueAt;
            }
            else
            {
                Assert.Fail($"{c.Id}/{fixtureName} is a snapshot case with no blessed outcome. Run with GOLDEN_UPDATE=1 to bless, then review the diff of outcomes/filters.json.");
            }
        }

        return (c, actual, expectedList.ToHashSet());
    }

    private static void AssertSetsEqual(OutcomeCase c, string fixtureName, HashSet<long> expected, HashSet<long> actual)
    {
        if (expected.SetEquals(actual)) return;

        var missing = expected.Except(actual).OrderBy(t => t).ToList();   // reference says true, engine says false
        var extra = actual.Except(expected).OrderBy(t => t).ToList();     // engine says true, reference says false
        Assert.Fail(
            $"{c.Id} ({c.Script}) on {fixtureName}: expected {expected.Count} true bars, engine produced {actual.Count}.\n" +
            $"  reference-only ({missing.Count}): {string.Join(", ", missing.Take(8).Select(Fmt))}{(missing.Count > 8 ? ", …" : "")}\n" +
            $"  engine-only    ({extra.Count}): {string.Join(", ", extra.Take(8).Select(Fmt))}{(extra.Count > 8 ? ", …" : "")}");
    }

    private static string Fmt(long ts) =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(ts), TimeZoneInfo.FindSystemTimeZoneById("America/New_York")).ToString("MM-dd HH:mm");

    private static string OutcomesPath => Path.Combine(GoldenFixture.GoldenRoot, "outcomes", "filters.json");

    private static OutcomesFile LoadOutcomes()
    {
        Assert.True(File.Exists(OutcomesPath), $"missing {OutcomesPath} — run tools/golden/compute_outcomes.py");
        return JsonSerializer.Deserialize<OutcomesFile>(File.ReadAllText(OutcomesPath), JsonOptions)!;
    }

    /// <summary>Writes a blessed snapshot outcome back to the *source* filters.json (not the bin copy).</summary>
    private static void Bless(string caseId, string fixtureName, List<long> trueAt)
    {
        lock (BlessLock)
        {
            var sourcePath = FindSourceOutcomesPath();
            var doc = JsonSerializer.Deserialize<OutcomesFile>(File.ReadAllText(sourcePath), JsonOptions)!;
            doc.Cases.Single(x => x.Id == caseId).Expected[fixtureName] = trueAt;
            File.WriteAllText(sourcePath, JsonSerializer.Serialize(doc, JsonOptions) + "\n");
            // keep the in-memory copy consistent for the rest of the run
            Outcomes.Value.Cases.Single(x => x.Id == caseId).Expected[fixtureName] = trueAt;
        }
    }

    private static string FindSourceOutcomesPath()
    {
        // bin/Debug/net10.0/TestData/Golden/outcomes/filters.json -> <project>/TestData/Golden/outcomes/filters.json
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MarketViewer.Filters.UnitTests.csproj")))
        {
            dir = dir.Parent;
        }
        if (dir is null) throw new InvalidOperationException("Could not locate the test project directory to bless outcomes.");
        return Path.Combine(dir.FullName, "TestData", "Golden", "outcomes", "filters.json");
    }

    public sealed class OutcomesFile
    {
        [JsonPropertyName("generatedBy")] public string GeneratedBy { get; set; } = "";
        [JsonPropertyName("cases")] public List<OutcomeCase> Cases { get; set; } = new();
    }

    public sealed class OutcomeCase
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("script")] public string Script { get; set; } = "";
        [JsonPropertyName("kind")] public string Kind { get; set; } = "reference";
        [JsonPropertyName("knownBug")] public string? KnownBug { get; set; }
        [JsonPropertyName("note")] public string Note { get; set; } = "";
        [JsonPropertyName("evaluatedCount")] public Dictionary<string, int> EvaluatedCount { get; set; } = new();
        [JsonPropertyName("expected")] public Dictionary<string, List<long>?> Expected { get; set; } = new();
    }
}
