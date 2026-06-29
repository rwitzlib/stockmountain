using Amazon.S3;
using Amazon.S3.Model;
using MarketViewer.Api.Authorization;
using MarketViewer.Api.Controllers.Market;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.MarketData;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Requests.Tools;
using MarketViewer.Contracts.Responses.Market;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Polygon.Client.Models;
using System.Text.Json;
using StatsResponse = MarketViewer.Contracts.Responses.Tools.StatsResponse;

namespace MarketViewer.Api.Controllers.Tools;

[ApiController]
[Authorize]
[Route("api/tools")]
public class ToolsController(
    IHttpContextAccessor contextAccessor,
    IMarketCache marketCache,
    IMemoryCache memoryCache,
    IAmazonS3 s3,
    IMediator mediator,
    ILogger<ToolsController> logger) : ControllerBase
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [HttpGet]
    [Route("aggregate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [RequiredPermissions([UserRole.Admin])]
    public IActionResult Aggregate([FromQuery] ToolsAggregateRequest request)
    {
        try
        {
            request.UserId = contextAccessor.HttpContext.Items["UserId"].ToString();

            var response = marketCache.GetStocksResponse(request.Ticker, new Timeframe(1, request.Timespan), DateTimeOffset.Now);

            return Ok(response);
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new List<string> { "Internal error." });
        }
    }

    [HttpGet]
    [Route("aggregate/compare/{year}/{month}/{day}/{ticker}/{timespan}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [RequiredPermissions([UserRole.Admin])]
    public async Task<IActionResult> S3Aggregate(string year, string month, string day, string ticker, Timespan timespan)
    {
        try
        {
            var start = new DateTimeOffset(int.Parse(year), int.Parse(month), int.Parse(day), 8, 30, 0, 0, DateTimeOffset.Now.Offset);
            var end = new DateTimeOffset(int.Parse(year), int.Parse(month), int.Parse(day), 15, 0, 0, 0, DateTimeOffset.Now.Offset);
            var liveKey = $"{year}-0{month}-{day}-{timespan.ToString().ToCharArray()[0]}-stocks.json";
            using var liveS3Response = await s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = Environment.GetEnvironmentVariable("MARKET_DATA_BUCKET_NAME") ?? MarketDataStorageContract.DefaultBucketName,
                Key = liveKey
            });

            using var liveReader = new StreamReader(liveS3Response.ResponseStream);
            var liveJson = liveReader.ReadToEnd();
            var liveResponse = JsonSerializer.Deserialize<IEnumerable<StocksResponse>>(liveJson, _jsonSerializerOptions);

            var liveStocksResponse = liveResponse.FirstOrDefault(q => q.Ticker == ticker);
            var liveResults = liveStocksResponse.Results.Where(q => q.Timestamp >= start.ToUnixTimeMilliseconds() && q.Timestamp <= end.ToUnixTimeMilliseconds());
            liveStocksResponse.Results = liveResults.ToList();

            var backtestKey = MarketDataStorageContract.BuildAggregateKey(new DateTimeOffset(int.Parse(year), int.Parse(month), int.Parse(day), 0, 0, 0, DateTimeOffset.Now.Offset), 1, timespan);
            using var backtestS3Response = await s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = Environment.GetEnvironmentVariable("MARKET_DATA_BUCKET_NAME") ?? MarketDataStorageContract.DefaultBucketName,
                Key = backtestKey
            });

            using var backtestReader = new StreamReader(backtestS3Response.ResponseStream);
            var backtestJson = backtestReader.ReadToEnd();
            var backtestResponse = JsonSerializer.Deserialize<IEnumerable<StocksResponse>>(backtestJson, _jsonSerializerOptions);

            var backtestStocksResponse = backtestResponse.FirstOrDefault(q => q.Ticker == ticker);
            var backtestResults = backtestStocksResponse.Results.Where(q => q.Timestamp >= start.ToUnixTimeMilliseconds() && q.Timestamp <= end.ToUnixTimeMilliseconds());
            backtestStocksResponse.Results = backtestResults.ToList();

            return Ok(new
            {
                Live = liveStocksResponse,
                Backtest = backtestStocksResponse
            });
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new List<string> { "Internal error." });
        }
    }

    [HttpPost]
    [Route("scan")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [RequiredPermissions([UserRole.Admin])]
    public async Task<IActionResult> Scan([FromBody] ToolsScanRequest request)
    {
        try
        {
            var response = await mediator.Send(request, CancellationToken.None);

            return Ok(response.Data);
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new List<string> { "Internal error." });
        }
    }

    [HttpGet]
    [Route("stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [RequiredPermissions([UserRole.Admin])]
    public IActionResult Stats()
    {
        try
        {
            contextAccessor.HttpContext.Items["UserId"].ToString();

            var stats = memoryCache.GetCurrentStatistics();
            var tickers = marketCache.GetTickers();

            var response = new StatsResponse
            {
                CacheStatistics = stats,
                TickerCount = tickers is null ? 0 : tickers.Count(),
                StocksResponseCount = 0
            };

            if (tickers is not null)
            {
                foreach (var ticker in tickers)
                {
                    var minuteResponse = marketCache.GetStocksResponse(ticker, new Timeframe(1, Timespan.minute), DateTimeOffset.Now);
                    var hourResponse = marketCache.GetStocksResponse(ticker, new Timeframe(1, Timespan.hour), DateTimeOffset.Now);

                    if (minuteResponse is not null)
                    {
                        response.StocksResponseCount++;
                    }

                    if (hourResponse is not null)
                    {
                        response.StocksResponseCount++;
                    }
                }
            }

            return Ok(response);
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new List<string> { "Internal error." });
        }
    }

    [HttpGet]
    [Route("websocket/live/SPY")]
    [RequiredPermissions([UserRole.Admin])]
    public IActionResult GetWebsocketResponses()
    {
        return Ok(memoryCache.Get<List<Bar>>("SPY_LIVE"));
    }

    [HttpGet]
    [Route("websocket/{tickers}")]
    [RequiredPermissions([UserRole.Admin])]
    public IActionResult GetWebsocketResponses(string tickers)
    {
        var tickersList = tickers.Split(',');

        List<StocksResponse> responses = [];
        foreach (var ticker in tickersList)
        {
            var bar = marketCache.GetLiveBar(ticker);

            responses.Add(new StocksResponse
            {
                Ticker = ticker,
                Status = "OK",
                Results = new List<Bar> { bar }
            });
        }

        return Ok(responses);
    }

    [HttpPost]
    [Route("filter")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [RequiredPermissions([UserRole.Basic, UserRole.Advanced, UserRole.Premium, UserRole.Admin])]
    public async Task<IActionResult> FilterAggregate([FromBody] ToolsFilterRequest request)
    {
        try
        {
            contextAccessor.HttpContext.Items["UserId"].ToString();
            var response = await mediator.Send(request);
            return Ok(response);
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new List<string> { "Internal error." });
        }
    }
}
