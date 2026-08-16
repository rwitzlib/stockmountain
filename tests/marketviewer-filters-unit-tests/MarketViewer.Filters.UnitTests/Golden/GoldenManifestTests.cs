using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace MarketViewer.Filters.UnitTests.Golden;

/// <summary>
/// Guards fixture integrity: every bars file listed in TestData/Golden/manifest.json must exist
/// and hash to the recorded sha256, every bars file on disk must be in the manifest, and every
/// bars file must have a reference file. A fixture cannot drift (or be hand-edited) without
/// re-running tools/golden/fetch_fixtures.py + compute_reference.py.
/// </summary>
public class GoldenManifestTests
{
    private sealed class Manifest
    {
        public Dictionary<string, Entry> Fixtures { get; set; } = new();
    }

    private sealed class Entry
    {
        public string File { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public int BarCount { get; set; }
    }

    private static Manifest LoadManifest()
    {
        var path = Path.Combine(GoldenFixture.GoldenRoot, "manifest.json");
        Assert.True(File.Exists(path), $"missing {path}");
        return JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    [Fact]
    public void Every_Manifest_Entry_Matches_File_On_Disk()
    {
        var manifest = LoadManifest();
        Assert.NotEmpty(manifest.Fixtures);

        foreach (var (name, entry) in manifest.Fixtures)
        {
            var path = Path.Combine(GoldenFixture.GoldenRoot, entry.File.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"{name}: {entry.File} missing");

            var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
            Assert.True(string.Equals(actual, entry.Sha256, StringComparison.OrdinalIgnoreCase),
                $"{name}: sha256 mismatch — fixture edited without re-running tools/golden/fetch_fixtures.py");

            var fixture = GoldenFixture.Load(name);
            Assert.Equal(entry.BarCount, fixture.Bars.Results.Count);
        }
    }

    [Fact]
    public void Every_Bars_File_Is_In_Manifest_And_Has_Reference()
    {
        var manifest = LoadManifest();
        var barsDir = Path.Combine(GoldenFixture.GoldenRoot, "bars");
        var onDisk = Directory.EnumerateFiles(barsDir, "*.json").Select(Path.GetFileNameWithoutExtension).ToList();

        Assert.NotEmpty(onDisk);
        foreach (var name in onDisk)
        {
            Assert.True(manifest.Fixtures.ContainsKey(name!), $"{name} not in manifest.json");
            Assert.True(File.Exists(Path.Combine(GoldenFixture.GoldenRoot, "reference", name + ".indicators.json")),
                $"{name} has no reference file — run tools/golden/compute_reference.py");
        }
    }
}
