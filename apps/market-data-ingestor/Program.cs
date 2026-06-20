using Npgsql;
using StockMountain.MarketData;
using StockMountain.MarketData.Catalog;
using StockMountain.MarketData.Catalog.Postgres;
using StockMountain.MarketData.Ingestion;
using StockMountain.MarketData.Ingestion.Massive;
using StockMountain.MarketData.Reading;
using StockMountain.MarketData.Storage;
using System.CommandLine;
using System.Globalization;

DotNetEnv.Env.Load();

var rootCommand = new RootCommand("StockMountain market data ingestor");

var symbolOption = CreateSymbolOption();
var fromOption = CreateTimestampOption("--from", "Start timestamp inclusive (ISO 8601 UTC)");
var toOption = CreateTimestampOption("--to", "End timestamp inclusive (ISO 8601 UTC)");
var timeframeOption = CreateTimeframeOption();
var adjustmentOption = CreateAdjustmentOption();

var backfillCommand = new Command("backfill", "Import historical bars for one Bar Series");
AddCommonOptions(backfillCommand, symbolOption, fromOption, toOption, timeframeOption, adjustmentOption);
backfillCommand.SetAction(async parseResult =>
{
    await using var context = await IngestorRunContext.CreateAsync(parseResult, symbolOption, fromOption, toOption, timeframeOption, adjustmentOption);
    var pipeline = new BackfillPipeline(context.Fetcher, new MonthlyBarFileWriter(context.ObjectStore, context.Catalog));

    var files = await pipeline.RunAsync(new BackfillRequest(context.Series, context.From, context.To));
    Console.WriteLine($"Backfill complete. Updated {files.Count} monthly file(s).");
    foreach (var file in files)
    {
        Console.WriteLine(
            $"  {file.ObjectKey} ({file.BarCount} bars, {file.EarliestBarStart:o}..{file.LatestBarStart:o})");
    }
});

var strictOption = new Option<bool>("--strict")
{
    Description = "Require full file-span coverage for the requested window",
    DefaultValueFactory = _ => false,
};
var limitOption = new Option<int?>("--limit")
{
    Description = "Maximum number of bars to print",
};

var readCommand = new Command("read", "Read historical bars for one Bar Series");
AddCommonOptions(readCommand, symbolOption, fromOption, toOption, timeframeOption, adjustmentOption);
readCommand.Options.Add(strictOption);
readCommand.Options.Add(limitOption);
readCommand.SetAction(async parseResult =>
{
    await using var context = await IngestorRunContext.CreateAsync(parseResult, symbolOption, fromOption, toOption, timeframeOption, adjustmentOption);
    var reader = new BarSeriesReader(context.Catalog, context.ObjectStore);
    var strict = parseResult.GetValue(strictOption);
    var limit = parseResult.GetValue(limitOption);

    var bars = strict
        ? reader.ReadAsync(context.Series, context.From, context.To)
        : reader.ReadAvailableAsync(context.Series, context.From, context.To);

    var count = 0;
    NormalizedBar? first = null;
    NormalizedBar? last = null;

    await foreach (var bar in bars)
    {
        first ??= bar;
        last = bar;
        count++;

        if (limit is not null && count <= limit)
        {
            Console.WriteLine(
                $"{bar.TimestampUtc:o} O={bar.Open} H={bar.High} L={bar.Low} C={bar.Close} V={bar.Volume}");
        }
    }

    Console.WriteLine($"Read {count} bar(s).");
    if (first is not null && last is not null)
    {
        Console.WriteLine($"First: {first.Value.TimestampUtc:o}");
        Console.WriteLine($"Last:  {last.Value.TimestampUtc:o}");
    }
});

rootCommand.Subcommands.Add(backfillCommand);
rootCommand.Subcommands.Add(readCommand);
return await rootCommand.Parse(args).InvokeAsync();

static Option<string> CreateSymbolOption() => new("--symbol")
{
    Description = "Ticker symbol",
    Required = true,
};

static Option<string> CreateTimestampOption(string name, string description) => new(name)
{
    Description = description,
    Required = true,
};

static Option<string> CreateTimeframeOption() => new("--timeframe")
{
    Description = "Timeframe path segment",
    DefaultValueFactory = _ => "1d",
};

static Option<string> CreateAdjustmentOption() => new("--adjustment")
{
    Description = "Adjustment policy path segment",
    DefaultValueFactory = _ => "split-adjusted",
};

static void AddCommonOptions(
    Command command,
    Option<string> symbolOption,
    Option<string> fromOption,
    Option<string> toOption,
    Option<string> timeframeOption,
    Option<string> adjustmentOption)
{
    command.Options.Add(symbolOption);
    command.Options.Add(fromOption);
    command.Options.Add(toOption);
    command.Options.Add(timeframeOption);
    command.Options.Add(adjustmentOption);
}

internal static class IngestorCommandParsing
{
    public static DateTimeOffset ParseTimestamp(string value)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        throw new ArgumentException($"Could not parse timestamp '{value}'. Use ISO 8601 format, e.g. 2026-01-15T14:30:00Z.");
    }

    public static BarSeriesKey CreateSeriesKey(string symbol, string timeframe, string adjustment)
    {
        var adjustmentPolicy = adjustment switch
        {
            "split-adjusted" => AdjustmentPolicy.SplitAdjusted,
            "unadjusted" => AdjustmentPolicy.Unadjusted,
            _ => throw new ArgumentException($"Unsupported adjustment policy '{adjustment}'."),
        };

        return new BarSeriesKey(StockMountain.MarketData.Symbol.Create(symbol), Timeframe.ParsePathSegment(timeframe), adjustmentPolicy);
    }
}

internal sealed class IngestorRunContext : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly HttpClient _httpClient;

    private IngestorRunContext(
        BarSeriesKey series,
        DateTimeOffset from,
        DateTimeOffset to,
        NpgsqlDataSource dataSource,
        PostgresBarSeriesCatalog catalog,
        S3BarObjectStore objectStore,
        HttpClient httpClient,
        MassiveBarFetcher fetcher)
    {
        Series = series;
        From = from;
        To = to;
        _dataSource = dataSource;
        Catalog = catalog;
        ObjectStore = objectStore;
        _httpClient = httpClient;
        Fetcher = fetcher;
    }

    public BarSeriesKey Series { get; }

    public DateTimeOffset From { get; }

    public DateTimeOffset To { get; }

    public PostgresBarSeriesCatalog Catalog { get; }

    public S3BarObjectStore ObjectStore { get; }

    public MassiveBarFetcher Fetcher { get; }

    public static async Task<IngestorRunContext> CreateAsync(
        ParseResult parseResult,
        Option<string> symbolOption,
        Option<string> fromOption,
        Option<string> toOption,
        Option<string> timeframeOption,
        Option<string> adjustmentOption)
    {
        var config = IngestorConfiguration.Load();
        var dataSource = PostgresDataSourceFactory.Create(config.DatabaseUrl);
        await PostgresBarSeriesCatalog.EnsureSchemaAsync(dataSource);

        var httpClient = new HttpClient { BaseAddress = config.MassiveBaseAddress };
        var fetcher = new MassiveBarFetcher(httpClient, new MassiveBarFetcherOptions { ApiKey = config.MassiveApiKey });

        return new IngestorRunContext(
            IngestorCommandParsing.CreateSeriesKey(
                parseResult.GetValue(symbolOption)!,
                parseResult.GetValue(timeframeOption)!,
                parseResult.GetValue(adjustmentOption)!),
            IngestorCommandParsing.ParseTimestamp(parseResult.GetValue(fromOption)!),
            IngestorCommandParsing.ParseTimestamp(parseResult.GetValue(toOption)!),
            dataSource,
            new PostgresBarSeriesCatalog(dataSource),
            new S3BarObjectStore(new S3BarObjectStoreOptions
            {
                BucketName = config.S3Bucket,
                ServiceUrl = config.AwsEndpointUrl,
                ForcePathStyle = config.AwsForcePathStyle,
            }),
            httpClient,
            fetcher);
    }

    public async ValueTask DisposeAsync()
    {
        ObjectStore.Dispose();
        await _dataSource.DisposeAsync();
        _httpClient.Dispose();
    }
}

internal sealed class IngestorConfiguration
{
    public required string MassiveApiKey { get; init; }

    public required Uri MassiveBaseAddress { get; init; }

    public required string S3Bucket { get; init; }

    public required string DatabaseUrl { get; init; }

    public string? AwsEndpointUrl { get; init; }

    public bool AwsForcePathStyle { get; init; }

    public static IngestorConfiguration Load()
    {
        var massiveApiKey = Required("MASSIVE_API_KEY");
        var s3Bucket = Required("S3_BUCKET");
        var databaseUrl = Required("DATABASE_URL");

        return new IngestorConfiguration
        {
            MassiveApiKey = massiveApiKey,
            MassiveBaseAddress = new Uri(Environment.GetEnvironmentVariable("MASSIVE_BASE_URL") ?? "https://api.massive.com"),
            S3Bucket = s3Bucket,
            DatabaseUrl = databaseUrl,
            AwsEndpointUrl = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL"),
            AwsForcePathStyle = string.Equals(
                Environment.GetEnvironmentVariable("AWS_S3_FORCE_PATH_STYLE"),
                "true",
                StringComparison.OrdinalIgnoreCase),
        };
    }

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Missing required environment variable '{name}'.");
}
