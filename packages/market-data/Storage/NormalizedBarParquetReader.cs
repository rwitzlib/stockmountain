using System.Runtime.CompilerServices;
using Parquet;

namespace StockMountain.MarketData.Storage;

public static class NormalizedBarParquetReader
{
    public static async Task<IReadOnlyList<NormalizedBar>> ReadAllAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        var bars = new List<NormalizedBar>();
        await foreach (var bar in ReadRangeAsync(
                           source,
                           DateTimeOffset.MinValue,
                           DateTimeOffset.MaxValue,
                           cancellationToken))
        {
            bars.Add(bar);
        }

        return bars;
    }

    public static async IAsyncEnumerable<NormalizedBar> ReadRangeAsync(
        Stream source,
        DateTimeOffset from,
        DateTimeOffset to,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (to < from)
        {
            throw new ArgumentException("to must not be before from.", nameof(to));
        }

        var fromUtc = from.ToUniversalTime();
        var toUtc = to.ToUniversalTime();

        var (seekableSource, ownsStream) = await PrepareStreamAsync(source, cancellationToken);
        try
        {
            await using var reader = await ParquetReader.CreateAsync(seekableSource, cancellationToken: cancellationToken);
            var schema = reader.Schema;

            var timestampField = schema.FindDataField(NormalizedBarParquetSchema.TimestampUtcField);
            var openField = schema.FindDataField(NormalizedBarParquetSchema.OpenField);
            var highField = schema.FindDataField(NormalizedBarParquetSchema.HighField);
            var lowField = schema.FindDataField(NormalizedBarParquetSchema.LowField);
            var closeField = schema.FindDataField(NormalizedBarParquetSchema.CloseField);
            var volumeField = schema.FindDataField(NormalizedBarParquetSchema.VolumeField);
            var vwapField = schema.FindDataField(NormalizedBarParquetSchema.VwapField);
            var transactionCountField = schema.FindDataField(NormalizedBarParquetSchema.TransactionCountField);

            for (var groupIndex = 0; groupIndex < reader.RowGroupCount; groupIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var rowGroup = reader.OpenRowGroupReader(groupIndex);
                var rowCount = checked((int)rowGroup.RowCount);

                var timestamps = new DateTime[rowCount];
                var opens = new decimal[rowCount];
                var highs = new decimal[rowCount];
                var lows = new decimal[rowCount];
                var closes = new decimal[rowCount];
                var volumes = new long[rowCount];
                var vwaps = new decimal[rowCount];
                var transactionCounts = new int[rowCount];

                await rowGroup.ReadAsync<DateTime>(timestampField, timestamps.AsMemory(), cancellationToken: cancellationToken);
                await rowGroup.ReadAsync<decimal>(openField, opens.AsMemory(), cancellationToken: cancellationToken);
                await rowGroup.ReadAsync<decimal>(highField, highs.AsMemory(), cancellationToken: cancellationToken);
                await rowGroup.ReadAsync<decimal>(lowField, lows.AsMemory(), cancellationToken: cancellationToken);
                await rowGroup.ReadAsync<decimal>(closeField, closes.AsMemory(), cancellationToken: cancellationToken);
                await rowGroup.ReadAsync<long>(volumeField, volumes.AsMemory(), cancellationToken: cancellationToken);
                await rowGroup.ReadAsync<decimal>(vwapField, vwaps.AsMemory(), cancellationToken: cancellationToken);
                await rowGroup.ReadAsync<int>(transactionCountField, transactionCounts.AsMemory(), cancellationToken: cancellationToken);

                for (var i = 0; i < rowCount; i++)
                {
                    var timestamp = new DateTimeOffset(DateTime.SpecifyKind(timestamps[i], DateTimeKind.Utc));
                    if (timestamp < fromUtc || timestamp > toUtc)
                    {
                        continue;
                    }

                    yield return new NormalizedBar(
                        timestamp,
                        opens[i],
                        highs[i],
                        lows[i],
                        closes[i],
                        volumes[i],
                        vwaps[i],
                        transactionCounts[i]);
                }
            }
        }
        finally
        {
            if (ownsStream)
            {
                await seekableSource.DisposeAsync();
            }
        }
    }

    private static async Task<(Stream Stream, bool OwnsStream)> PrepareStreamAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        if (source.CanSeek)
        {
            return (source, false);
        }

        var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        return (buffer, true);
    }
}
