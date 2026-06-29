using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polygon.Client.Interfaces;
using Polygon.Client.Models;
using Polygon.Client.Requests;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Kesha;

public class Function(IServiceProvider serviceProvider)
{
    private readonly IPolygonClient _polygonClient = serviceProvider.GetService<IPolygonClient>();
    private readonly IAmazonS3 _s3Client = serviceProvider.GetService<IAmazonS3>();

    public Function() : this(Startup.ConfigureServices()) { }

    public async Task FunctionHandler(TickerDetailsRequest request, ILambdaContext context)
    {
        try
        {
            if (request is null || string.IsNullOrEmpty(request.Date) || !DateTime.TryParse(request.Date, out var date))
            {
                context.Logger.LogInformation("Invalid date.");
                return;
            }

            context.Logger.LogInformation($"Getting all {request.Market} tickers for {date.Date:yyyy-MM-dd}");

            var timer = new Stopwatch();
            timer.Start();

            var getTickersRequest = new PolygonGetTickersRequest
            {
                Market = request.Market,
                Type = "CS,ETF"
            };

            var getTickersResponse = await _polygonClient.GetTickers(getTickersRequest);

            context.Logger.LogInformation($"Found {getTickersResponse.Results.Count()} tickers.");

            var tickerDetailsList = new List<TickerDetails>();

            int batchSize = 850;
            for (int i = 0; i < getTickersResponse.Results.Count(); i += batchSize)
            {
                var tasks = new List<Task<TickerDetails>>();
                context.Logger.LogInformation($"i: {i}");
                var batch = getTickersResponse.Results.Take(new Range(i, i + batchSize));

                foreach (var tickerDetails in batch)
                {
                    tasks.Add(Task.Run(async () => await GetTickerDetailsAsync(tickerDetails.Ticker)));
                }
                var results = await Task.WhenAll(tasks);
                var validResults = results.Where(tickerDetails => tickerDetails is not null);
                context.Logger.LogInformation($"Found {validResults.Count()} valid TickerDetails results.");

                tickerDetailsList.AddRange(validResults);
            }

            context.Logger.LogInformation($"Found {tickerDetailsList.Count} aggregates.");

            if (tickerDetailsList.Count <= 0)
            {
                return;
            }

            var json = JsonSerializer.Serialize(tickerDetailsList);

            var response = await _s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = "lad-dev-marketviewer",
                Key = $"tickerdetails/{request.Market}.json",
                //Key = $"tickerdetails/{date.Year}/{date.Month}/{date.Month}-{date.Day}.json",
                ContentBody = json
            });

            timer.Stop();

            if (response is not null && response.HttpStatusCode.Equals(HttpStatusCode.OK))
            {
                context.Logger.LogInformation($"Successfully uploaded backtest data for {date.Date:yyyy-MM-dd} in {timer.ElapsedMilliseconds} ms.");
            }
            else
            {
                context.Logger.LogInformation($"Failed to upload backtest data for {date.Date:yyyy-MM-dd} in {timer.ElapsedMilliseconds} ms.");
            }
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error: {ex.Message}");
        }
        finally
        {
            GC.Collect();
        }
    }

    private async Task<TickerDetails> GetTickerDetailsAsync(string ticker)
    {
        if (string.IsNullOrEmpty(ticker))
        {
            return null;
        }

        try
        {
            var response = await _polygonClient.GetTickerDetails(ticker);

            if (response is not null && response.TickerDetails is not null)
            {
                return response.TickerDetails;
            }

            return null;
        }
        catch (Exception ex)
        {
            return null;
        }
    }
}
