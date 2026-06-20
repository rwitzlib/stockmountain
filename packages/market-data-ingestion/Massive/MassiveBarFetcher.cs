using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StockMountain.MarketData.Ingestion.Massive;

public sealed class MassiveBarFetcherOptions
{
    public required string ApiKey { get; init; }

    public Uri BaseAddress { get; init; } = new("https://api.massive.com");

    public int PageSize { get; init; } = 50_000;
}

public sealed class MassiveBarFetcher : IHistoricalBarFetcher
{
    private static readonly JsonSerializerOptions JsonOptions = MassiveJsonSerializerOptions.Create();

    private readonly HttpClient _httpClient;
    private readonly MassiveBarFetcherOptions _options;

    public MassiveBarFetcher(HttpClient httpClient, MassiveBarFetcherOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new ArgumentException("ApiKey is required.", nameof(options));
        }
    }

    public async Task<IReadOnlyList<NormalizedBar>> FetchAsync(
        BarSeriesKey series,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        series.Validate();
        EnsureSupportedSeries(series);

        var path = BuildInitialPath(series, from, to);
        var bars = new List<NormalizedBar>();
        string? nextPath = path;

        while (!string.IsNullOrWhiteSpace(nextPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var request = new HttpRequestMessage(HttpMethod.Get, nextPath);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<MassiveAggregatesResponse>(JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("Massive aggregates response was empty.");

            if (payload.Results is not null)
            {
                foreach (var result in payload.Results)
                {
                    bars.Add(MassiveAggregateBarNormalizer.ToNormalizedBar(result, series.Timeframe));
                }
            }

            nextPath = ResolveNextPath(payload.NextUrl);
        }

        return bars;
    }

    private string BuildInitialPath(BarSeriesKey series, DateTimeOffset from, DateTimeOffset to)
    {
        var (multiplier, timespan) = ToMassiveTimespan(series.Timeframe);
        var adjusted = series.AdjustmentPolicy == AdjustmentPolicy.SplitAdjusted ? "true" : "false";
        var fromUtc = from.ToUniversalTime();
        var toUtc = to.ToUniversalTime();
        var (fromValue, toValue) = series.Timeframe.Unit == TimeframeUnit.Day
            ? (fromUtc.ToString("yyyy-MM-dd"), toUtc.ToString("yyyy-MM-dd"))
            : (fromUtc.ToUnixTimeMilliseconds().ToString(), toUtc.ToUnixTimeMilliseconds().ToString());

        return $"/v2/aggs/ticker/{series.Symbol.Value}/range/{multiplier}/{timespan}/{fromValue}/{toValue}" +
               $"?adjusted={adjusted}&sort=asc&limit={_options.PageSize}";
    }

    private static (int Multiplier, string Timespan) ToMassiveTimespan(Timeframe timeframe) =>
        timeframe.Unit switch
        {
            TimeframeUnit.Day => (timeframe.Multiplier, "day"),
            TimeframeUnit.Minute => (timeframe.Multiplier, "minute"),
            _ => throw new NotSupportedException($"Massive timeframe unit '{timeframe.Unit}' is not supported."),
        };

    private string? ResolveNextPath(string? nextUrl)
    {
        if (string.IsNullOrWhiteSpace(nextUrl))
        {
            return null;
        }

        if (Uri.TryCreate(nextUrl, UriKind.Absolute, out var absolute))
        {
            if (absolute.Host.Equals(_options.BaseAddress.Host, StringComparison.OrdinalIgnoreCase))
            {
                return absolute.PathAndQuery;
            }

            return absolute.ToString();
        }

        return nextUrl.StartsWith('/') ? nextUrl : $"/{nextUrl}";
    }

    internal static void EnsureSupportedSeries(BarSeriesKey series)
    {
        if (series.AdjustmentPolicy != AdjustmentPolicy.SplitAdjusted)
        {
            throw new NotSupportedException("Only split-adjusted bars are supported in the first ingest milestones.");
        }

        if (series.Timeframe.Unit is not (TimeframeUnit.Day or TimeframeUnit.Minute))
        {
            throw new NotSupportedException("Only daily and minute-based timeframes are supported.");
        }
    }

    private sealed class MassiveAggregatesResponse
    {
        [JsonPropertyName("results")]
        public List<MassiveAggregateBar>? Results { get; init; }

        [JsonPropertyName("next_url")]
        public string? NextUrl { get; init; }
    }
}

public sealed class MassiveAggregateBar
{
    [JsonPropertyName("t")]
    [JsonConverter(typeof(FlexibleInt64JsonConverter))]
    public long TimestampMilliseconds { get; init; }

    [JsonPropertyName("o")]
    public decimal Open { get; init; }

    [JsonPropertyName("h")]
    public decimal High { get; init; }

    [JsonPropertyName("l")]
    public decimal Low { get; init; }

    [JsonPropertyName("c")]
    public decimal Close { get; init; }

    [JsonPropertyName("v")]
    [JsonConverter(typeof(FlexibleInt64JsonConverter))]
    public long Volume { get; init; }

    [JsonPropertyName("vw")]
    public decimal Vwap { get; init; }

    [JsonPropertyName("n")]
    [JsonConverter(typeof(FlexibleInt32JsonConverter))]
    public int TransactionCount { get; init; }
}
