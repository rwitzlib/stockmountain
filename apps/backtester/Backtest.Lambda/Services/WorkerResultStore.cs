using Amazon.S3;
using Amazon.S3.Model;
using Backtest.Lambda.Utilities;
using MarketViewer.Contracts.Responses.Market.Backtest;
using MarketViewer.Infrastructure.Config;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backtest.Lambda.Services;

/// <summary>
/// Hands each worker day's full <see cref="WorkerResponse"/> from the worker lambda to the
/// orchestrator through S3, keeping the invocation response itself to a small pointer
/// (<see cref="Models.WorkerResultLocation"/>). Objects live under workerResults/ so a
/// lifecycle expiration rule can target them independently of the filter cache
/// (strategyEntries/) and the persisted backtest output (backtestResults/).
/// </summary>
public class WorkerResultStore(IAmazonS3 s3, BacktestConfig config)
{
    // Both sides of the handoff serialize with these options; the lambda payload
    // serializer's conventions are irrelevant to the stored document.
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public async Task<(string Key, int StoredBytes)> Put(string backtestId, DateTimeOffset date, WorkerResponse response)
    {
        var key = BuildKey(backtestId, date);
        var body = Serialize(response);

        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = config.S3BucketName,
            Key = key,
            ContentBody = body
        });

        return (key, body.Length);
    }

    public async Task<WorkerResponse> Get(string key)
    {
        var s3Object = await s3.GetObjectAsync(config.S3BucketName, key);

        using var reader = new StreamReader(s3Object.ResponseStream);
        var content = await reader.ReadToEndAsync();

        return Deserialize(content);
    }

    /// <summary>
    /// A retried invocation of the same day overwrites the same key, so a successful
    /// attempt always leaves exactly one current object per (backtest, day).
    /// </summary>
    internal static string BuildKey(string backtestId, DateTimeOffset date)
    {
        var id = string.IsNullOrWhiteSpace(backtestId) ? "adhoc" : backtestId;
        return $"workerResults/{id}/{date:yyyy-MM-dd}";
    }

    internal static string Serialize(WorkerResponse response)
    {
        return CompressionUtilities.CompressString(JsonSerializer.Serialize(response, Options));
    }

    internal static WorkerResponse Deserialize(string content)
    {
        return CompressionUtilities.DecompressString<WorkerResponse>(content, Options);
    }
}
