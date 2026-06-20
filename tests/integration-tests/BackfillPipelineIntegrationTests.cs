using StockMountain.MarketData;
using StockMountain.MarketData.Catalog;
using StockMountain.MarketData.Catalog.Postgres;
using StockMountain.MarketData.Ingestion;
using StockMountain.MarketData.Storage;
using Testcontainers.PostgreSql;

namespace StockMountain.IntegrationTests;

public class BackfillPipelineIntegrationTests
{
    [Fact]
    public async Task RunAsync_WritesMergedBarsToS3CompatibleStoreAndPostgresCatalog()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();

        await postgres.StartAsync();

        await using var dataSource = Npgsql.NpgsqlDataSource.Create(postgres.GetConnectionString());
        await PostgresBarSeriesCatalog.EnsureSchemaAsync(dataSource);

        var catalog = new PostgresBarSeriesCatalog(dataSource);
        var objectStore = CreateObjectStore();
        var writer = new MonthlyBarFileWriter(objectStore, catalog);
        var fetcher = new StubFetcher([
            new NormalizedBar(new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero), 1, 2, 0.5m, 1.5m, 100, 1.1m, 10),
            new NormalizedBar(new DateTimeOffset(2024, 2, 2, 0, 0, 0, TimeSpan.Zero), 2, 3, 1.5m, 2.5m, 200, 2.1m, 20),
        ]);

        var pipeline = new BackfillPipeline(fetcher, writer);
        var files = await pipeline.RunAsync(new BackfillRequest(
            new BarSeriesKey(Symbol.Create("MSFT"), new Timeframe(1, TimeframeUnit.Day), AdjustmentPolicy.SplitAdjusted),
            new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 2, 2, 23, 59, 59, TimeSpan.Zero)));

        Assert.Single(files);
        Assert.True(await objectStore.ExistsAsync(files[0].ObjectKey));

        var registered = await catalog.GetFilesAsync(
            files[0].Series,
            files[0].PeriodStart,
            files[0].PeriodEnd);

        Assert.Single(registered);
        Assert.Equal(2, registered[0].BarCount);
    }

    private static IBarObjectStore CreateObjectStore()
    {
        var endpoint = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");
        var bucket = Environment.GetEnvironmentVariable("S3_BUCKET") ?? "stockmountain-test-bars";

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return new InMemoryBarObjectStore();
        }

        return new S3BarObjectStore(new S3BarObjectStoreOptions
        {
            BucketName = bucket,
            ServiceUrl = endpoint,
            ForcePathStyle = true,
        });
    }

    private static bool ShouldRunIntegrationTests() =>
        string.Equals(
            Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    private sealed class StubFetcher(IReadOnlyList<NormalizedBar> bars) : IHistoricalBarFetcher
    {
        public Task<IReadOnlyList<NormalizedBar>> FetchAsync(
            BarSeriesKey series,
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(bars);
    }
}
