using System.Text.Json;
using System.Text.RegularExpressions;
using MarketViewer.Filters.Expressions;
using MarketViewer.Filters.Interfaces;
using MarketViewer.Filters.Registry;
using MarketViewer.Filters.UnitTests.Golden;
using Xunit;

namespace MarketViewer.Filters.UnitTests.Registry;

/// <summary>
/// The "definition of done" for a filter function, enforced (plan 15). Every token in
/// <see cref="FunctionRegistry"/> must have complete metadata, parse, be documented under
/// docs/filters/, be covered by the golden suite and — for series indicators — implement the
/// incremental interface. If you are adding a function and one of these fails, the failure
/// message tells you what is missing; see .claude/skills/add-filter-function/SKILL.md.
/// </summary>
public class RegistryParityTests
{
    private static readonly IndicatorExpressionEngine Engine = new();

    /// <summary>
    /// Series functions with no independent Python oracle. Their correctness is pinned by blessed
    /// snapshot outcome cases instead of reference series. Add here only with a written justification.
    /// </summary>
    private static readonly HashSet<string> SnapshotOnlySeries = new(StringComparer.OrdinalIgnoreCase)
    {
        // Zone-mapping algorithm is bespoke; blessed via GoldenFilterOutcomeTests "sr-*" cases.
        "support_resistance",
    };

    /// <summary>
    /// Pre-plan-15 series functions that still recompute in full on every bar. New series
    /// indicators MUST implement IIncrementalSeriesFunction — do not extend this list.
    /// </summary>
    private static readonly HashSet<string> LegacyNonIncremental = new(StringComparer.OrdinalIgnoreCase)
    {
        // Windowed zone scan over `lookback` bars; incremental form tracked in plans/11-strategy-dsl-gaps.md.
        "support_resistance",
    };

    public static IEnumerable<object[]> AllTokens() => FunctionRegistry.All.Select(d => new object[] { d.Name });
    public static IEnumerable<object[]> FunctionTokens() => FunctionRegistry.Functions.Select(d => new object[] { d.Name });

    private static FunctionDescriptor Get(string name)
    {
        Assert.True(FunctionRegistry.TryGet(name, out var d), $"registry has no token '{name}'");
        return d;
    }

    // ------------------------------------------------------------------ registry integrity

    [Fact]
    public void Registry_HasEveryFunctionTheParserKnows()
    {
        // The parser table is built from the registry, so this is a smoke check that reflection
        // found the classes at all (a bad attribute placement would silently drop a function).
        var expected = new[] { "sma", "ema", "macd", "rsi", "adv", "vwap", "slope", "crosses_over", "crosses_under", "support_resistance" };
        foreach (var name in expected)
            Assert.True(FunctionRegistry.TryGetFunction(name, out _), $"expected function '{name}' missing from registry");
        Assert.True(FunctionRegistry.TryGetFunction("sr", out var sr) && sr.Name == "support_resistance", "alias 'sr' should resolve");
        Assert.Equal(7, KeywordRegistry.All.Count);
    }

    [Fact]
    public void Registry_NamesAreUniqueAndLowercase()
    {
        var names = FunctionRegistry.All.SelectMany(d => d.AllNames).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var n in names)
        {
            Assert.Matches("^[a-z][a-z0-9_]*$", n);
        }
    }

    [Theory]
    [MemberData(nameof(AllTokens))]
    public void Metadata_IsComplete(string name)
    {
        var d = Get(name);
        Assert.False(string.IsNullOrWhiteSpace(d.Signature), $"{name}: Signature is required");
        Assert.False(string.IsNullOrWhiteSpace(d.Snippet), $"{name}: Snippet is required");
        Assert.False(string.IsNullOrWhiteSpace(d.Description), $"{name}: Description is required");
        Assert.True(d.Cost > 0, $"{name}: Cost must be > 0");
        Assert.True(d.Selectivity is > 0 and <= 1, $"{name}: Selectivity must be in (0, 1]");
        Assert.NotEqual(FilterContext.None, d.Contexts);
        Assert.True(d.Contexts.HasFlag(FilterContext.Scan) || d.Contexts.HasFlag(FilterContext.Backtest),
            $"{name}: every DSL token must be valid in at least one filter context");
        if (!d.IsKeyword)
        {
            Assert.StartsWith(d.Name + "(", d.Signature);
            Assert.StartsWith(d.Name + "(", d.Snippet);
        }
    }

    [Theory]
    [MemberData(nameof(AllTokens))]
    public void Snippet_Parses(string name)
    {
        var d = Get(name);
        // Snippets are inserted by autocomplete; they must at least parse as an operand.
        var script = d.Kind == FunctionKind.Boolean ? d.Snippet : $"{d.Snippet} > 0";
        var parsed = Engine.ParseExpression(script);
        Assert.NotNull(parsed);
    }

    [Theory]
    [MemberData(nameof(FunctionTokens))]
    public void Kind_MatchesImplementedInterfaces(string name)
    {
        var d = Get(name);
        switch (d.Kind)
        {
            case FunctionKind.Series:
                Assert.True(d.IsSeriesFunction, $"{name}: Kind=Series must implement ISeriesFunction");
                if (!LegacyNonIncremental.Contains(d.Name))
                    Assert.True(d.IsIncremental, $"{name}: series indicators must implement IIncrementalSeriesFunction (plan 15 decision 5)");
                break;
            case FunctionKind.Transform:
                Assert.True(d.IsSeriesFunction, $"{name}: Kind=Transform must implement ISeriesFunction");
                Assert.True(d.IsIncremental, $"{name}: transforms must implement IIncrementalSeriesFunction");
                break;
            case FunctionKind.Boolean:
                Assert.True(d.IsBooleanFunction, $"{name}: Kind=Boolean must implement IBooleanFunction");
                break;
            default:
                Assert.Fail($"{name}: unexpected kind {d.Kind} for a function class");
                break;
        }
    }

    [Theory]
    [MemberData(nameof(FunctionTokens))]
    public void Fields_MatchResultType(string name)
    {
        var d = Get(name);
        if (d.Fields.Count == 0 || d.Kind == FunctionKind.Boolean) return;

        // Execute the snippet on a golden fixture and compare the declared fields with what the
        // result type actually exposes.
        var fixture = GoldenFixture.Load(GoldenFixture.Names().First(n => n.Contains("_1d_")));
        var context = new ExpressionContext
        {
            StockData = fixture.CloneBars(),
            Timeframe = new MarketViewer.Contracts.Models.Timeframe(1, MarketViewer.Contracts.Enums.Timespan.day),
        };
        var series = Assert.IsType<List<IIndicatorResult>>(Engine.ParseExpression(d.Snippet).Evaluate(context));
        var point = series.LastOrDefault();
        Assert.NotNull(point);
        var actual = point!.GetAvailableFields().ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var f in d.Fields)
            Assert.True(actual.Contains(f), $"{name}: declared field '{f}' is not exposed by {point.GetType().Name} ({string.Join(", ", actual)})");
    }

    [Theory]
    [MemberData(nameof(FunctionTokens))]
    public void Heuristics_ComeFromTheRegistry(string name)
    {
        var d = Get(name);
        var (cost, selectivity) = FunctionHeuristicsRegistry.GetHeuristics(name);
        Assert.Equal(d.Cost, cost);
        Assert.Equal(d.Selectivity, selectivity);
    }

    // ------------------------------------------------------------------ docs

    [Theory]
    [MemberData(nameof(AllTokens))]
    public void Docs_PageExistsAndFrontmatterMatches(string name)
    {
        var d = Get(name);
        var path = Path.Combine(RepoRoot.Value, "docs", "filters", $"{d.Name}.md");
        Assert.True(File.Exists(path), $"{name}: missing user docs page {path} — see .claude/skills/add-filter-function/references/docs-template.md");

        var text = File.ReadAllText(path);
        var fm = Frontmatter(text, path);
        Assert.Equal(d.Name, fm.GetValueOrDefault("name"));
        Assert.Equal(d.Kind.ToString().ToLowerInvariant(), fm.GetValueOrDefault("kind"));
        Assert.Equal(d.Signature, fm.GetValueOrDefault("signature"));

        var declared = ParseList(fm.GetValueOrDefault("contexts"));
        var expected = new List<string>();
        if (d.Contexts.HasFlag(FilterContext.Scan)) expected.Add("scan");
        if (d.Contexts.HasFlag(FilterContext.Backtest)) expected.Add("backtest");
        if (d.Contexts.HasFlag(FilterContext.Chart)) expected.Add("chart");
        Assert.Equal(expected, declared);

        foreach (var heading in new[] { "## Signature", "## Examples" })
            Assert.Contains(heading, text);
        if (d.Kind is FunctionKind.Series or FunctionKind.Transform)
        {
            Assert.Contains("## Formula", text);
            Assert.Contains("## Warm-up", text);
        }
    }

    [Fact]
    public void Docs_NoOrphanPages()
    {
        var dir = Path.Combine(RepoRoot.Value, "docs", "filters");
        var pages = Directory.EnumerateFiles(dir, "*.md")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null && n != "index")
            .Cast<string>()
            .ToList();
        var orphans = pages.Where(p => !FunctionRegistry.TryGet(p, out var d) || !string.Equals(d.Name, p, StringComparison.Ordinal)).ToList();
        Assert.True(orphans.Count == 0, $"docs/filters pages without a registered function: {string.Join(", ", orphans)}");
    }

    // ------------------------------------------------------------------ golden coverage

    [Theory]
    [MemberData(nameof(AllTokens))]
    public void Golden_CoversToken(string name)
    {
        var d = Get(name);
        var referenceKeys = GoldenReferenceKeys.Value;
        var outcomeScripts = GoldenOutcomeScripts.Value;

        if (d.Kind is FunctionKind.Series or FunctionKind.Transform && !SnapshotOnlySeries.Contains(d.Name))
        {
            var hasKey = referenceKeys.Any(k => k.StartsWith(d.Name + "(", StringComparison.OrdinalIgnoreCase));
            Assert.True(hasKey, $"{name}: no reference series key '{d.Name}(…)' in TestData/Golden/reference/*.indicators.json — add it to tools/golden/compute_reference.py and re-run");
        }
        else
        {
            var pattern = d.IsKeyword ? $@"\b{Regex.Escape(d.Name)}\b" : $@"\b{Regex.Escape(d.Name)}\s*\(";
            var used = outcomeScripts.Any(s => Regex.IsMatch(s, pattern, RegexOptions.IgnoreCase));
            Assert.True(used, $"{name}: no golden outcome case uses it — add a Case(...) to tools/golden/compute_outcomes.py and re-run");
        }
    }

    [Fact]
    public void Golden_ReferenceKeysAndOutcomeScriptsParse()
    {
        foreach (var key in GoldenReferenceKeys.Value)
            Engine.ParseExpression($"{key} > 0");
        foreach (var script in GoldenOutcomeScripts.Value)
            Engine.ParseExpression(script);
    }

    // ------------------------------------------------------------------ helpers

    private static readonly Lazy<string> RepoRoot = new(() =>
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "docs", "filters")) && Directory.Exists(Path.Combine(dir.FullName, "packages")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate repo root (looked for docs/filters + packages/ above the test bin directory)");
    });

    private static readonly Lazy<HashSet<string>> GoldenReferenceKeys = new(() =>
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in GoldenFixture.Names())
            keys.UnionWith(GoldenFixture.Load(name).Reference.Series.Keys);
        return keys;
    });

    private static readonly Lazy<List<string>> GoldenOutcomeScripts = new(() =>
    {
        var path = Path.Combine(GoldenFixture.GoldenRoot, "outcomes", "filters.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty("cases").EnumerateArray()
            .Select(c => c.GetProperty("script").GetString()!)
            .ToList();
    });

    private static Dictionary<string, string> Frontmatter(string text, string path)
    {
        var m = Regex.Match(text, @"\A---\r?\n(?<body>.*?)\r?\n---", RegexOptions.Singleline);
        Assert.True(m.Success, $"{path}: missing YAML frontmatter block");
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in m.Groups["body"].Value.Split('\n'))
        {
            var idx = line.IndexOf(':');
            if (idx <= 0) continue;
            dict[line[..idx].Trim()] = line[(idx + 1)..].Trim().Trim('"');
        }
        return dict;
    }

    private static List<string> ParseList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Trim('[', ']').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
