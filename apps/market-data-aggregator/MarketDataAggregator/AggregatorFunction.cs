using Amazon.DynamoDBv2;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using FluentValidation;
using MarketDataAggregator.Validation;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.MarketData;
using MarketViewer.Contracts.Records.MarketData;
using MarketViewer.Contracts.Responses.Market;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Massive.Client.Interfaces;
using Massive.Client.Models;
using Massive.Client.Requests;
using Massive.Client.Responses;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MarketDataAggregator;

public class AggregatorFunction(IServiceProvider serviceProvider)
{
    private readonly IMassiveClient _massiveClient = serviceProvider.GetRequiredService<IMassiveClient>();
    private readonly IAmazonS3 _s3Client = serviceProvider.GetRequiredService<IAmazonS3>();
    private readonly IAmazonDynamoDB _dynamoDb = serviceProvider.GetRequiredService<IAmazonDynamoDB>();
    private readonly ILogger<AggregatorFunction> _logger = serviceProvider.GetRequiredService<ILogger<AggregatorFunction>>();
    private readonly IValidator<MarketDataAggregatorRequest> _requestValidator = serviceProvider.GetRequiredService<IValidator<MarketDataAggregatorRequest>>();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly int _batchSize = int.TryParse(Environment.GetEnvironmentVariable("BATCH_SIZE"), out var batchCount) ? batchCount : 30;
    private readonly string _marketDataBucketName = Environment.GetEnvironmentVariable("MARKET_DATA_BUCKET_NAME") ?? MarketDataStorageContract.DefaultBucketName;
    private readonly TimeZoneInfo _timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    public AggregatorFunction() : this(Startup.ConfigureServices()) { }

    public async Task FunctionHandler(MarketDataAggregatorRequest? request, ILambdaContext context)
    {
        _logger.LogInformation("Starting Market Data Aggregator: {date}, {multiplier}, {timespan}", request?.Date, request?.Multiplier, request?.Timespan);

        var startedAt = DateTimeOffset.UtcNow;
        if (request is null)
        {
            _logger.LogInformation("Invalid request. Must contain a type or date.");
            return;
        }

        if (!MarketDataRequestNormalizer.TryPrepareAggregatorRequest(request))
        {
            _logger.LogInformation("Invalid request. Must either contain a type of \"auto\" or a date range.");
            return;
        }

        var validationResult = _requestValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            LogValidationErrors(validationResult);
            return;
        }

        var catalog = new MarketDataCatalogWriter(_dynamoDb, _logger);
        var key = MarketDataStorageContract.BuildAggregateKey(request.Date, request.Multiplier, request.Timespan);

        await catalog.PutInventoryRecord(new MarketDataInventoryRecord
        {
            Date = request.Date,
            Multiplier = request.Multiplier,
            Timespan = request.Timespan,
            Bucket = _marketDataBucketName,
            Key = key,
            Status = MarketDataStatus.Running,
            StartedAt = startedAt,
            Source = request.Source,
            RunId = request.RunId
        });

        try
        {
            var tickers = await GetAndUploadTickers();
            var result = await GetAndUploadAggregates(request, tickers);

            await catalog.PutInventoryRecord(new MarketDataInventoryRecord
            {
                Date = request.Date,
                Multiplier = request.Multiplier,
                Timespan = request.Timespan,
                Bucket = _marketDataBucketName,
                Key = key,
                Status = MarketDataStatus.Succeeded,
                ObjectSize = result.ObjectSize,
                ETag = result.ETag,
                RecordCount = result.RecordCount,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow,
                Source = request.Source,
                RunId = request.RunId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during Market Data Aggregator.");
            await catalog.PutInventoryRecord(new MarketDataInventoryRecord
            {
                Date = request.Date,
                Multiplier = request.Multiplier,
                Timespan = request.Timespan,
                Bucket = _marketDataBucketName,
                Key = key,
                Status = MarketDataStatus.Failed,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow,
                Source = request.Source,
                RunId = request.RunId,
                Error = ex.Message
            });
        }
    }

    private async Task<List<TickerDetails>> GetAndUploadTickers()
    {
        var timer = Stopwatch.StartNew();

        var tickers = new List<TickerDetails>();
        var stocksTickersResponse = await _massiveClient.GetTickers(new MassiveGetTickersRequest
        {
            Market = "stocks",
            Active = true,
            Type = "CS"
        });
        tickers.AddRange(stocksTickersResponse.Results);

        var etfTickersResponse = await _massiveClient.GetTickers(new MassiveGetTickersRequest
        {
            Market = "stocks",
            Active = true,
            Type = "ETF"
        });
        tickers.AddRange(etfTickersResponse.Results);

        _logger.LogInformation("Found {count} tickers.", tickers.Count);

        var tickerDetailsList = new List<TickerDetails>();

        foreach (var batch in tickers.Chunk(_batchSize))
        {
            var results = await Task.WhenAll(batch.Select(tickerDetails => GetTickerDetailsAsync(tickerDetails.Ticker)));
            var validResults = results.OfType<TickerDetails>();
            _logger.LogInformation("Found {count} valid TickerDetails results.", validResults.Count());

            tickerDetailsList.AddRange(validResults);
        }

        if (tickerDetailsList.Count <= 0)
        {
            return [];
        }

        await AttachFloats(tickerDetailsList);

        var json = JsonSerializer.Serialize(tickerDetailsList);
        var response = await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _marketDataBucketName,
            Key = MarketDataStorageContract.TickerDetailsKey,
            ContentType = "application/json",
            ContentBody = json
        });

        timer.Stop();

        _logger.LogInformation("Successfully uploaded ticker details for {date} in {elapsed} ms.", DateTimeOffset.Now.Date.ToString("yyyy-MM-dd"), timer.ElapsedMilliseconds);

        return tickerDetailsList;
    }

    private async Task AttachFloats(List<TickerDetails> tickerDetailsList)
    {
        var floatResponse = await _massiveClient.GetFloats();

        if (floatResponse.Status != HttpStatusCode.OK.ToString() || !floatResponse.Results.Any())
        {
            _logger.LogWarning("Unable to retrieve floats from Massive API. Status: {status}", floatResponse.Status);
            return;
        }

        var floatsByTicker = floatResponse.Results
            .Where(stockFloat => stockFloat.Ticker is not null)
            .GroupBy(stockFloat => stockFloat.Ticker!)
            .ToDictionary(group => group.Key, group => group.First().FreeFloat);

        var matched = 0;
        foreach (var tickerDetails in tickerDetailsList)
        {
            if (tickerDetails.Ticker is not null && floatsByTicker.TryGetValue(tickerDetails.Ticker, out var freeFloat))
            {
                tickerDetails.Float = freeFloat;
                matched++;
            }
        }

        _logger.LogInformation("Attached float to {matched} of {total} tickers.", matched, tickerDetailsList.Count);
    }

    private async Task<AggregateUploadResult> GetAndUploadAggregates(MarketDataAggregatorRequest request, List<TickerDetails> tickers)
    {
        var key = MarketDataStorageContract.BuildAggregateKey(request.Date, request.Multiplier, request.Timespan);
        var stocksResponses = new List<StocksResponse>();

        foreach (var batch in tickers.Chunk(_batchSize))
        {
            var results = await Task.WhenAll(batch.Select(ticker => GetAggregateAsync(request, ticker)));
            var validResults = results.OfType<MassiveAggregateResponse>();

            _logger.LogInformation("Found {count} valid aggregate results.", validResults.Count());

            stocksResponses.AddRange(validResults.Select(MapToStocksResponse));
        }

        _logger.LogInformation("Found {count} aggregate responses.", stocksResponses.Count);

        // Hour and day objects span a whole month/year, so a run for a date in the middle
        // of that period must not drop data already stored past its fetch window.
        if (request.Timespan is Timespan.hour or Timespan.day && !request.Overwrite)
        {
            var existing = await GetExistingAggregates(key);

            if (existing is not null && existing.Count > 0)
            {
                var fetchWindowEnd = GetEndDate(request.Date).ToUnixTimeMilliseconds();
                stocksResponses = AggregateMerger.Merge(stocksResponses, existing, fetchWindowEnd);

                _logger.LogInformation("Merged with existing object {key}; {count} responses after merge.", key, stocksResponses.Count);
            }
        }

        var json = JsonSerializer.Serialize(stocksResponses);
        var response = await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _marketDataBucketName,
            Key = key,
            ContentType = "application/json",
            ContentBody = json
        });

        if (response is not null && response.HttpStatusCode.Equals(HttpStatusCode.OK))
        {
            _logger.LogInformation("Successfully uploaded market data for {date}.", request.Date.Date);
        }
        else
        {
            throw new InvalidOperationException($"Failed to upload market data for {request.Date.Date:yyyy-MM-dd}.");
        }

        return new AggregateUploadResult(stocksResponses.Count, response.ETag, json.Length);
    }

    private void LogValidationErrors(FluentValidation.Results.ValidationResult validationResult)
    {
        foreach (var error in validationResult.Errors)
        {
            var message = error.ErrorMessage.StartsWith("No market data to gather on", StringComparison.Ordinal)
                ? error.ErrorMessage
                : $"Invalid request. {error.ErrorMessage}";

            _logger.LogInformation("{Message}", message);
        }
    }

    private async Task<List<StocksResponse>?> GetExistingAggregates(string key)
    {
        try
        {
            using var response = await _s3Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _marketDataBucketName,
                Key = key
            });
            using var streamReader = new StreamReader(response.ResponseStream);

            var json = await streamReader.ReadToEndAsync();

            return JsonSerializer.Deserialize<List<StocksResponse>>(json, SerializerOptions);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<TickerDetails?> GetTickerDetailsAsync(string ticker)
    {
        if (string.IsNullOrEmpty(ticker))
        {
            return null;
        }

        try
        {
            var response = await _massiveClient.GetTickerDetails(ticker);

            return response?.TickerDetails;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving TickerDetails for ticker: {ticker}", ticker);
            return null;
        }
    }

    private async Task<MassiveAggregateResponse?> GetAggregateAsync(MarketDataAggregatorRequest request, TickerDetails ticker)
    {
        var endOfDay = GetEndDate(request.Date).ToUnixTimeMilliseconds();

        var response = await _massiveClient.GetAggregates(new MassiveAggregateRequest
        {
            Ticker = ticker.Ticker,
            Multiplier = request.Multiplier,
            Timespan = request.Timespan.ToString(),
            From = GetStartDate(request.Timespan, request.Date).ToString(),
            To = endOfDay.ToString()
        });

        if (response?.Results is null)
        {
            return null;
        }

        return response;
    }

    private long GetStartDate(Timespan timespan, DateTimeOffset date)
    {
        var offset = _timeZone.GetUtcOffset(date);

        var startDate = timespan switch
        {
            Timespan.minute => new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, offset),
            Timespan.hour => new DateTimeOffset(date.Year, date.Month, 1, 0, 0, 0, offset),
            Timespan.day => new DateTimeOffset(date.Year, 1, 1, 0, 0, 0, offset),
            _ => throw new NotSupportedException($"Timespan {timespan} is not supported.")
        };

        return startDate.ToUnixTimeMilliseconds();
    }

    private DateTimeOffset GetEndDate(DateTimeOffset date)
    {
        var offset = _timeZone.GetUtcOffset(date);
        return new DateTimeOffset(date.Year, date.Month, date.Day, 23, 59, 0, offset);
    }

    private static StocksResponse MapToStocksResponse(MassiveAggregateResponse response)
    {
        return new StocksResponse
        {
            Ticker = response.Ticker,
            Status = response.Status,
            Results = response.Results.ToList()
        };
    }

    private record AggregateUploadResult(int RecordCount, string ETag, long ObjectSize);
}
