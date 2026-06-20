using Npgsql;

namespace StockMountain.MarketData.Catalog.Postgres;

public sealed class PostgresBarSeriesCatalog : IBarSeriesCatalog
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresBarSeriesCatalog(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public PostgresBarSeriesCatalog(string connectionString)
        : this(PostgresDataSourceFactory.Create(connectionString))
    {
    }

    public static async Task EnsureSchemaAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(BarSeriesCatalogSchema.Sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BarSeriesFile>> GetFilesAsync(
        BarSeriesKey series,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        series.Validate();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT object_key, period_start, period_end, earliest_bar_start, latest_bar_start, bar_count
            FROM bar_series_files
            WHERE symbol = @symbol
              AND timeframe_multiplier = @multiplier
              AND timeframe_unit = @unit
              AND adjustment_policy = @policy
              AND earliest_bar_start <= @to
              AND latest_bar_start >= @from
            ORDER BY earliest_bar_start
            """,
            connection);

        BindSeries(command, series);
        command.Parameters.AddWithValue("from", from.ToUniversalTime());
        command.Parameters.AddWithValue("to", to.ToUniversalTime());

        var files = new List<BarSeriesFile>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            files.Add(ReadBarSeriesFile(series, reader));
        }

        return files;
    }

    public async Task<BarSeriesCoverage?> GetCoverageAsync(
        BarSeriesKey series,
        CancellationToken cancellationToken = default)
    {
        series.Validate();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT MIN(earliest_bar_start), MAX(latest_bar_start)
            FROM bar_series_files
            WHERE symbol = @symbol
              AND timeframe_multiplier = @multiplier
              AND timeframe_unit = @unit
              AND adjustment_policy = @policy
            """,
            connection);

        BindSeries(command, series);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0))
        {
            return null;
        }

        return new BarSeriesCoverage(
            series,
            reader.GetFieldValue<DateTimeOffset>(0),
            reader.GetFieldValue<DateTimeOffset>(1));
    }

    public async Task RegisterFileAsync(
        BarSeriesFile file,
        CancellationToken cancellationToken = default)
    {
        file.Validate();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO bar_series_files (
                object_key,
                symbol,
                timeframe_multiplier,
                timeframe_unit,
                adjustment_policy,
                period_start,
                period_end,
                earliest_bar_start,
                latest_bar_start,
                bar_count,
                registered_at)
            VALUES (
                @object_key,
                @symbol,
                @multiplier,
                @unit,
                @policy,
                @period_start,
                @period_end,
                @earliest_bar_start,
                @latest_bar_start,
                @bar_count,
                NOW())
            ON CONFLICT (object_key) DO UPDATE SET
                period_start = EXCLUDED.period_start,
                period_end = EXCLUDED.period_end,
                earliest_bar_start = EXCLUDED.earliest_bar_start,
                latest_bar_start = EXCLUDED.latest_bar_start,
                bar_count = EXCLUDED.bar_count,
                registered_at = NOW()
            """,
            connection);

        command.Parameters.AddWithValue("object_key", file.ObjectKey);
        BindSeries(command, file.Series);
        command.Parameters.AddWithValue("period_start", file.PeriodStart.ToUniversalTime());
        command.Parameters.AddWithValue("period_end", file.PeriodEnd.ToUniversalTime());
        command.Parameters.AddWithValue("earliest_bar_start", file.EarliestBarStart.ToUniversalTime());
        command.Parameters.AddWithValue("latest_bar_start", file.LatestBarStart.ToUniversalTime());
        command.Parameters.AddWithValue("bar_count", (object?)file.BarCount ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DateRange>> FindGapsAsync(
        BarSeriesKey series,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        series.Validate();

        if (to <= from)
        {
            throw new ArgumentException("to must be after from.", nameof(to));
        }

        var files = await GetFilesAsync(series, from, to, cancellationToken);
        if (files.Count == 0)
        {
            return [new DateRange(from, to)];
        }

        var gaps = new List<DateRange>();
        var cursor = from;

        foreach (var file in files)
        {
            if (file.EarliestBarStart > cursor)
            {
                gaps.Add(new DateRange(cursor, Min(to, file.EarliestBarStart)));
            }

            cursor = Max(cursor, file.LatestBarStart.AddTicks(1));
            if (cursor >= to)
            {
                return gaps;
            }
        }

        if (cursor < to)
        {
            gaps.Add(new DateRange(cursor, to));
        }

        return gaps;
    }

    private static BarSeriesFile ReadBarSeriesFile(BarSeriesKey series, NpgsqlDataReader reader) =>
        new(
            series,
            reader.GetString(0),
            reader.GetFieldValue<DateTimeOffset>(1),
            reader.GetFieldValue<DateTimeOffset>(2),
            reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.IsDBNull(5) ? null : reader.GetInt64(5));

    private static void BindSeries(NpgsqlCommand command, BarSeriesKey series)
    {
        command.Parameters.AddWithValue("symbol", series.Symbol.Value);
        command.Parameters.AddWithValue("multiplier", series.Timeframe.Multiplier);
        command.Parameters.AddWithValue("unit", ToDbValue(series.Timeframe.Unit));
        command.Parameters.AddWithValue("policy", ToDbValue(series.AdjustmentPolicy));
    }

    private static string ToDbValue(TimeframeUnit unit) =>
        unit switch
        {
            TimeframeUnit.Second => "second",
            TimeframeUnit.Minute => "minute",
            TimeframeUnit.Hour => "hour",
            TimeframeUnit.Day => "day",
            TimeframeUnit.Week => "week",
            TimeframeUnit.Month => "month",
            TimeframeUnit.Quarter => "quarter",
            TimeframeUnit.Year => "year",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown timeframe unit."),
        };

    private static string ToDbValue(AdjustmentPolicy policy) =>
        policy switch
        {
            AdjustmentPolicy.SplitAdjusted => "split-adjusted",
            AdjustmentPolicy.Unadjusted => "unadjusted",
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown adjustment policy."),
        };

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) =>
        left <= right ? left : right;

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right) =>
        left >= right ? left : right;
}
