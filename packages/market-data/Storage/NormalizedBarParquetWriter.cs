using Parquet;

namespace StockMountain.MarketData.Storage;

public static class NormalizedBarParquetWriter
{
    public static async Task WriteAsync(
        Stream destination,
        IReadOnlyList<NormalizedBar> bars,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(bars);

        var schema = NormalizedBarParquetSchema.Create();
        await using var writer = await ParquetWriter.CreateAsync(schema, destination, cancellationToken: cancellationToken);
        using var rowGroup = writer.CreateRowGroup();

        var timestamps = new DateTime[bars.Count];
        var opens = new decimal[bars.Count];
        var highs = new decimal[bars.Count];
        var lows = new decimal[bars.Count];
        var closes = new decimal[bars.Count];
        var volumes = new long[bars.Count];
        var vwaps = new decimal[bars.Count];
        var transactionCounts = new int[bars.Count];

        for (var i = 0; i < bars.Count; i++)
        {
            var bar = bars[i];
            timestamps[i] = bar.TimestampUtc.UtcDateTime;
            opens[i] = bar.Open;
            highs[i] = bar.High;
            lows[i] = bar.Low;
            closes[i] = bar.Close;
            volumes[i] = bar.Volume;
            vwaps[i] = bar.Vwap;
            transactionCounts[i] = bar.TransactionCount;
        }

        await rowGroup.WriteAsync<DateTime>(schema.DataFields[0], timestamps.AsMemory(), cancellationToken: cancellationToken);
        await rowGroup.WriteAsync<decimal>(schema.DataFields[1], opens.AsMemory(), cancellationToken: cancellationToken);
        await rowGroup.WriteAsync<decimal>(schema.DataFields[2], highs.AsMemory(), cancellationToken: cancellationToken);
        await rowGroup.WriteAsync<decimal>(schema.DataFields[3], lows.AsMemory(), cancellationToken: cancellationToken);
        await rowGroup.WriteAsync<decimal>(schema.DataFields[4], closes.AsMemory(), cancellationToken: cancellationToken);
        await rowGroup.WriteAsync<long>(schema.DataFields[5], volumes.AsMemory(), cancellationToken: cancellationToken);
        await rowGroup.WriteAsync<decimal>(schema.DataFields[6], vwaps.AsMemory(), cancellationToken: cancellationToken);
        await rowGroup.WriteAsync<int>(schema.DataFields[7], transactionCounts.AsMemory(), cancellationToken: cancellationToken);
    }
}
