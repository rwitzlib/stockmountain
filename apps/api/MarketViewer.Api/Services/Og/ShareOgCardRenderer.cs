using System.Globalization;
using System.Reflection;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace MarketViewer.Api.Services.Og;

/// <summary>
/// Renders the 1200x630 Open Graph image for a shared backtest: dark report-style card
/// with the equity curve, SPY benchmark, and headline stats. Matches the web app's
/// dark theme tokens so link previews look like the product.
/// </summary>
public static class ShareOgCardRenderer
{
    private const int Width = 1200;
    private const int Height = 630;

    private static readonly Color Background = Color.ParseHex("14171C");
    private static readonly Color Foreground = Color.ParseHex("F2F4F7");
    private static readonly Color Muted = Color.ParseHex("94A3B8");
    private static readonly Color Grid = Color.FromRgba(148, 163, 184, 23);
    private static readonly Color Teal = Color.ParseHex("14A3BD");
    private static readonly Color TealFill = Color.FromRgba(20, 163, 189, 26);
    private static readonly Color Amber = Color.FromRgba(196, 129, 23, 217);
    private static readonly Color Gain = Color.ParseHex("2FAE60");
    private static readonly Color Loss = Color.ParseHex("E05252");

    private static readonly FontFamily Mono = LoadFontFamily();

    public static byte[] Render(ShareOgCard card)
    {
        var bold = Mono.CreateFont(30, FontStyle.Bold);
        var title = Mono.CreateFont(40, FontStyle.Bold);
        var big = Mono.CreateFont(72, FontStyle.Bold);
        var sub = Mono.CreateFont(20, FontStyle.Regular);
        var label = Mono.CreateFont(15, FontStyle.Regular);
        var kpi = Mono.CreateFont(30, FontStyle.Bold);
        var small = Mono.CreateFont(18, FontStyle.Regular);

        using var image = new Image<Rgba32>(Width, Height);
        image.Mutate(ctx =>
        {
            ctx.Fill(Background);

            for (var x = 48f; x < Width; x += 48f)
            {
                ctx.DrawLine(Grid, 1f, new PointF(x, 0), new PointF(x, Height));
            }

            for (var y = 48f; y < Height; y += 48f)
            {
                ctx.DrawLine(Grid, 1f, new PointF(0, y), new PointF(Width, y));
            }

            DrawWordmark(ctx, bold);
            ctx.DrawText(RightAligned(sub, 1136, 58), "SHARED BACKTEST", Muted);

            var displayTitle = TruncateToWidth(card.Title, title, 1072);
            ctx.DrawText(TopLeft(title, 64, 118), displayTitle, Foreground);

            var range = FormatRange(card);
            ctx.DrawText(TopLeft(sub, 64, 174), range, Muted);

            var gainColor = card.NetReturnPct >= 0 ? Gain : Loss;
            var netText = FormatSignedPercent(card.NetReturnPct, 1);
            ctx.DrawText(TopLeft(big, 64, 216), netText, gainColor);

            var netWidth = TextMeasurer.MeasureAdvance(netText, new TextOptions(big)).Width;
            var balanceText = $"{FormatMoney(card.StartingBalance)} → {FormatMoney(card.EndBalance)}";
            ctx.DrawText(TopLeft(sub, 64 + netWidth + 28, 268), balanceText, Muted);

            DrawChart(ctx, card, left: 64, right: 1136, top: 330, bottom: 500);

            DrawKpi(ctx, label, kpi, 64, "PROFIT FACTOR", card.ProfitFactor.ToString("F2", CultureInfo.InvariantCulture), Foreground);
            DrawKpi(ctx, label, kpi, 340, "SHARPE", card.SharpeRatio.ToString("F2", CultureInfo.InvariantCulture), Foreground);
            DrawKpi(ctx, label, kpi, 616, "WIN RATE", $"{card.WinRatioPct.ToString("F1", CultureInfo.InvariantCulture)}%", Foreground);
            DrawKpi(ctx, label, kpi, 892, "MAX DRAWDOWN", $"−{Math.Abs(card.MaxDrawdownPct).ToString("F2", CultureInfo.InvariantCulture)}%", Loss);

            ctx.DrawText(RightAligned(small, 1136, 594), "stockmountain.io", Muted);
        });

        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private static void DrawWordmark(IImageProcessingContext ctx, Font font)
    {
        // Same glyph as the SPA favicon: mountain polyline over a faint baseline.
        const float scale = 2.6f;
        const float originX = 64f;
        const float originY = 44f;
        PointF P(float x, float y) => new(originX + (x - 2f) * scale, originY + (y - 4f) * scale);

        var pen = new SolidPen(new PenOptions(Foreground, 5f)
        {
            JointStyle = JointStyle.Round,
            EndCapStyle = EndCapStyle.Round,
        });
        ctx.DrawLine(pen, P(2, 20), P(8.5f, 8), P(12.5f, 14), P(17.5f, 4), P(22, 20));

        var baselinePen = new SolidPen(new PenOptions(Color.FromRgba(242, 244, 247, 89), 5f)
        {
            EndCapStyle = EndCapStyle.Round,
        });
        ctx.DrawLine(baselinePen, P(2, 20), P(22, 20));

        var options = TopLeft(font, originX + 20f * scale + 20f, originY + 4f);
        ctx.DrawText(options, "StockMountain", Foreground);
    }

    private static void DrawChart(IImageProcessingContext ctx, ShareOgCard card, float left, float right, float top, float bottom)
    {
        var equity = Downsample(card.Equity, 400);
        var benchmark = Downsample(card.Benchmark, 400);

        // Both series drawn as growth relative to their own first point so they share one scale.
        var series = new List<float[]> { Normalize(equity) };
        if (benchmark.Count >= 2)
        {
            series.Add(Normalize(benchmark));
        }

        var min = series.SelectMany(s => s).Min();
        var max = series.SelectMany(s => s).Max();
        var pad = Math.Max((max - min) * 0.06f, 0.001f);
        min -= pad;
        max += pad;

        PointF[] ToPoints(float[] values)
        {
            var points = new PointF[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                var x = left + (right - left) * i / (values.Length - 1);
                var y = bottom - (bottom - top) * (values[i] - min) / (max - min);
                points[i] = new PointF(x, y);
            }

            return points;
        }

        var strategy = ToPoints(series[0]);

        var area = new PointF[strategy.Length + 2];
        strategy.CopyTo(area, 0);
        area[^2] = new PointF(right, bottom);
        area[^1] = new PointF(left, bottom);
        ctx.FillPolygon(TealFill, area);

        if (series.Count > 1)
        {
            var benchPen = new PatternPen(new PenOptions(Amber, 2.5f, [4f, 3f]));
            ctx.DrawLine(benchPen, ToPoints(series[1]));
        }

        var strategyPen = new SolidPen(new PenOptions(Teal, 4f)
        {
            JointStyle = JointStyle.Round,
            EndCapStyle = EndCapStyle.Round,
        });
        ctx.DrawLine(strategyPen, strategy);

        ctx.Fill(Teal, new EllipsePolygon(strategy[^1], 7f));
    }

    private static void DrawKpi(IImageProcessingContext ctx, Font labelFont, Font valueFont, float x, string label, string value, Color valueColor)
    {
        ctx.DrawText(TopLeft(labelFont, x, 536), label, Muted);
        ctx.DrawText(TopLeft(valueFont, x, 560), value, valueColor);
    }

    private static RichTextOptions TopLeft(Font font, float x, float y) => new(font)
    {
        Origin = new PointF(x, y),
    };

    private static RichTextOptions RightAligned(Font font, float x, float y) => new(font)
    {
        Origin = new PointF(x, y),
        HorizontalAlignment = HorizontalAlignment.Right,
    };

    private static string TruncateToWidth(string text, Font font, float maxWidth)
    {
        if (TextMeasurer.MeasureAdvance(text, new TextOptions(font)).Width <= maxWidth)
        {
            return text;
        }

        var truncated = text;
        while (truncated.Length > 1 &&
               TextMeasurer.MeasureAdvance(truncated + "…", new TextOptions(font)).Width > maxWidth)
        {
            truncated = truncated[..^1].TrimEnd();
        }

        return truncated + "…";
    }

    private static string FormatRange(ShareOgCard card)
    {
        var culture = CultureInfo.InvariantCulture;
        var range = card.Start != default && card.End != default
            ? $"{card.Start.ToString("MMM d, yyyy", culture)} — {card.End.ToString("MMM d, yyyy", culture)}"
            : "Backtest";
        return card.TotalTrades > 0 ? $"{range} · {card.TotalTrades} trades" : range;
    }

    internal static string FormatSignedPercent(float value, int decimals)
    {
        var sign = value >= 0 ? "+" : "−";
        return $"{sign}{Math.Abs(value).ToString($"F{decimals}", CultureInfo.InvariantCulture)}%";
    }

    private static string FormatMoney(float value)
        => "$" + Math.Round(value).ToString("N0", CultureInfo.InvariantCulture);

    private static float[] Normalize(IReadOnlyList<float> values)
    {
        var first = values[0] == 0 ? 1f : values[0];
        var result = new float[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            result[i] = values[i] / first;
        }

        return result;
    }

    private static IReadOnlyList<float> Downsample(IReadOnlyList<float> values, int maxPoints)
    {
        if (values.Count <= maxPoints)
        {
            return values;
        }

        var result = new List<float>(maxPoints);
        for (var i = 0; i < maxPoints; i++)
        {
            result.Add(values[(int)((long)i * (values.Count - 1) / (maxPoints - 1))]);
        }

        return result;
    }

    private static FontFamily LoadFontFamily()
    {
        var collection = new FontCollection();
        FontFamily family = default;
        foreach (var file in new[] { "JetBrainsMono-Regular.ttf", "JetBrainsMono-Bold.ttf" })
        {
            using var stream = OpenEmbedded(file);
            family = collection.Add(stream);
        }

        return family;
    }

    private static Stream OpenEmbedded(string fileName)
    {
        var assembly = typeof(ShareOgCardRenderer).Assembly;
        var resource = assembly.GetManifestResourceNames()
                           .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                       ?? throw new InvalidOperationException($"Embedded font not found: {fileName}");
        return assembly.GetManifestResourceStream(resource)!;
    }
}
