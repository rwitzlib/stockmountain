using System.Text.Json;

namespace MarketViewer.Api.Services.Og;

/// <summary>
/// The handful of values a link-preview card needs, extracted from a share payload.
/// Parsed with JsonDocument rather than the full contract types so the card keeps
/// working regardless of how optional config sections are encoded.
/// </summary>
public record ShareOgCard(
    string Title,
    DateTimeOffset Start,
    DateTimeOffset End,
    float StartingBalance,
    float EndBalance,
    float NetReturnPct,
    float ProfitFactor,
    float SharpeRatio,
    float WinRatioPct,
    float MaxDrawdownPct,
    int TotalTrades,
    IReadOnlyList<float> Equity,
    IReadOnlyList<float> Benchmark)
{
    public const string FallbackTitle = "Backtest report";

    /// <summary>Returns null when the payload can't back a meaningful card (unknown schema, no equity curve).</summary>
    public static ShareOgCard FromPayloadJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!TryGetInt(root, "schemaVersion", out var schemaVersion) || schemaVersion != 1)
        {
            return null;
        }

        if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object ||
            !result.TryGetProperty("hold", out var hold) || hold.ValueKind != JsonValueKind.Object ||
            !hold.TryGetProperty("equity", out var equityElement) || equityElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var equity = new List<float>();
        var firstStartCash = 0f;
        foreach (var point in equityElement.EnumerateArray())
        {
            if (TryGetFloat(point, "totalBalance", out var totalBalance))
            {
                equity.Add(totalBalance);
            }

            if (equity.Count == 1)
            {
                TryGetFloat(point, "startCash", out firstStartCash);
            }
        }

        if (equity.Count < 2)
        {
            return null;
        }

        // Same fallback chain as the share page: unmasked config, then day one's cash, then the default.
        var startingBalance = 0f;
        if (root.TryGetProperty("config", out var config) && config.ValueKind == JsonValueKind.Object &&
            config.TryGetProperty("positionSettings", out var positionSettings) && positionSettings.ValueKind == JsonValueKind.Object)
        {
            TryGetFloat(positionSettings, "startingBalance", out startingBalance);
        }

        if (startingBalance <= 0)
        {
            startingBalance = firstStartCash;
        }

        if (startingBalance <= 0)
        {
            startingBalance = 10_000f;
        }

        var stats = hold.TryGetProperty("stats", out var s) && s.ValueKind == JsonValueKind.Object ? s : default;
        TryGetFloat(stats, "endBalance", out var endBalance);
        if (endBalance <= 0)
        {
            endBalance = equity[^1];
        }

        TryGetFloat(stats, "profitFactor", out var profitFactor);
        TryGetFloat(stats, "sharpeRatio", out var sharpeRatio);
        TryGetFloat(stats, "winRatio", out var winRatio);
        TryGetFloat(stats, "maxDrawdown", out var maxDrawdown);
        TryGetInt(stats, "totalTradesTaken", out var totalTrades);

        var benchmark = new List<float>();
        if (root.TryGetProperty("benchmark", out var benchmarkElement) && benchmarkElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var point in benchmarkElement.EnumerateArray())
            {
                if (TryGetFloat(point, "close", out var close) && close > 0)
                {
                    benchmark.Add(close);
                }
            }
        }

        return new ShareOgCard(
            Title: GetString(root, "title") is { Length: > 0 } title ? title : FallbackTitle,
            Start: GetDate(root, "start"),
            End: GetDate(root, "end"),
            StartingBalance: startingBalance,
            EndBalance: endBalance,
            NetReturnPct: (endBalance / startingBalance - 1f) * 100f,
            ProfitFactor: profitFactor,
            SharpeRatio: sharpeRatio,
            WinRatioPct: winRatio * 100f,
            MaxDrawdownPct: maxDrawdown * 100f,
            TotalTrades: totalTrades,
            Equity: equity,
            Benchmark: benchmark);
    }

    private static string GetString(JsonElement parent, string name)
        => parent.ValueKind == JsonValueKind.Object &&
           parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static DateTimeOffset GetDate(JsonElement parent, string name)
        => parent.ValueKind == JsonValueKind.Object &&
           parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
           value.TryGetDateTimeOffset(out var date)
            ? date
            : default;

    private static bool TryGetFloat(JsonElement parent, string name, out float value)
    {
        if (parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Number &&
            element.TryGetSingle(out var single) && float.IsFinite(single))
        {
            value = single;
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryGetInt(JsonElement parent, string name, out int value)
    {
        if (parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Number &&
            element.TryGetInt32(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }
}
