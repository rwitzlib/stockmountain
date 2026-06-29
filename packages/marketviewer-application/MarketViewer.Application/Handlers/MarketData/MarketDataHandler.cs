using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.S3;
using Amazon.S3.Model;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Interfaces;
using MarketViewer.Contracts.MarketData;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Records.MarketData;
using MarketViewer.Contracts.Requests.MarketData;
using MarketViewer.Infrastructure.Config;
using System.Net;
using System.Text.Json;

namespace MarketViewer.Application.Handlers.MarketData;

public class MarketDataHandler(
    IMarketDataCatalogRepository repository,
    IAmazonLambda lambda,
    IAmazonS3 s3,
    MarketDataConfig config)
{
    public async Task<OperationResult<List<MarketDataInventoryRecord>>> ListInventory(MarketDataInventoryQueryRequest request)
    {
        if (request.Start == default || request.End == default || request.Start.Date > request.End.Date)
        {
            return BadRequest<List<MarketDataInventoryRecord>>("Start and end dates are required, and start must be before end.");
        }

        var records = await repository.ListInventory(request);
        return new OperationResult<List<MarketDataInventoryRecord>>
        {
            Status = HttpStatusCode.OK,
            Data = records
        };
    }

    public async Task<OperationResult<List<MarketDataRunRecord>>> ListRuns(int limit = 50)
    {
        var records = await repository.ListRuns(limit);
        return new OperationResult<List<MarketDataRunRecord>>
        {
            Status = HttpStatusCode.OK,
            Data = records
        };
    }

    public async Task<OperationResult<MarketDataRunRecord>> Backfill(MarketDataBackfillRequest request)
    {
        if (request.Start == default || request.End == default || request.Start.Date > request.End.Date)
        {
            return BadRequest<MarketDataRunRecord>("Start and end dates are required, and start must be before end.");
        }

        if (string.IsNullOrWhiteSpace(config.OrchestratorLambdaName))
        {
            return BadRequest<MarketDataRunRecord>("Market data orchestrator lambda is not configured.");
        }

        var run = new MarketDataRunRecord
        {
            RunId = Guid.NewGuid().ToString("N"),
            Start = request.Start,
            End = request.End,
            Timespans = request.Timespans,
            Multiplier = request.Multiplier,
            Source = "api",
            Status = MarketDataStatus.Pending,
            RequestedCount = CountWorkItems(request),
            StartedAt = DateTimeOffset.UtcNow
        };

        await repository.PutRunRecord(run);

        var payload = JsonSerializer.Serialize(new
        {
            run.RunId,
            request.Start,
            request.End,
            request.Timespans,
            request.Multiplier,
            request.MaxConcurrency,
            request.Overwrite,
            Source = "api"
        });

        await lambda.InvokeAsync(new InvokeRequest
        {
            FunctionName = config.OrchestratorLambdaName,
            InvocationType = InvocationType.Event,
            Payload = payload
        });

        return new OperationResult<MarketDataRunRecord>
        {
            Status = HttpStatusCode.Accepted,
            Data = run
        };
    }

    public async Task<OperationResult<List<MarketDataInventoryRecord>>> Reconcile(MarketDataInventoryQueryRequest request)
    {
        if (request.Start == default || request.End == default || request.Start.Date > request.End.Date)
        {
            return BadRequest<List<MarketDataInventoryRecord>>("Start and end dates are required, and start must be before end.");
        }

        var records = new List<MarketDataInventoryRecord>();
        var timespans = request.Timespan is null
            ? new List<Timespan> { Timespan.minute, Timespan.hour, Timespan.day }
            : new List<Timespan> { request.Timespan.Value };
        var multiplier = request.Multiplier ?? 1;
        var days = (request.End.Date - request.Start.Date).Days;

        for (var i = 0; i <= days; i++)
        {
            var date = request.Start.Date.AddDays(i);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            foreach (var timespan in timespans)
            {
                var key = MarketDataStorageContract.BuildAggregateKey(date, multiplier, timespan);
                var record = await ReconcileObject(date, multiplier, timespan, key);
                records.Add(record);
            }
        }

        return new OperationResult<List<MarketDataInventoryRecord>>
        {
            Status = HttpStatusCode.OK,
            Data = records
        };
    }

    private async Task<MarketDataInventoryRecord> ReconcileObject(DateTimeOffset date, int multiplier, Timespan timespan, string key)
    {
        var record = new MarketDataInventoryRecord
        {
            Date = date,
            Multiplier = multiplier,
            Timespan = timespan,
            Bucket = config.S3BucketName ?? MarketDataStorageContract.DefaultBucketName,
            Key = key,
            Source = "reconciliation"
        };

        try
        {
            var metadata = await s3.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = record.Bucket,
                Key = key
            });

            record.Status = MarketDataStatus.Succeeded;
            record.ObjectSize = metadata.ContentLength;
            record.ETag = metadata.ETag;
            record.CompletedAt = metadata.LastModified;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            record.Status = MarketDataStatus.Missing;
            record.Error = "Object not found.";
        }

        await repository.PutInventoryRecord(record);
        return record;
    }

    private static int CountWorkItems(MarketDataBackfillRequest request)
    {
        var days = (request.End.Date - request.Start.Date).Days;
        var tradingDays = Enumerable.Range(0, days + 1)
            .Select(offset => request.Start.Date.AddDays(offset))
            .Count(date => date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday);

        return tradingDays * request.Timespans.Count;
    }

    private static OperationResult<T> BadRequest<T>(string message)
    {
        return new OperationResult<T>
        {
            Status = HttpStatusCode.BadRequest,
            ErrorMessages = [message]
        };
    }
}
