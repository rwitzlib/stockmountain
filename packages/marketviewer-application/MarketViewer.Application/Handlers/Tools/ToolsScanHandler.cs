//using Amazon.S3;
//using Amazon.S3.Model;
//using MarketViewer.Contracts.Enums;
//using MarketViewer.Contracts.Enums.Scan;
//using MarketViewer.Contracts.Models;
//using MarketViewer.Contracts.Requests.Tools;
//using MarketViewer.Contracts.Responses.Market;
//using MarketViewer.Contracts.Responses.Tools;
//using MarketViewer.Core.Scan;
//using MarketViewer.Infrastructure.Utilities;
////using Microsoft.Extensions.Caching.Memory;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text.Json;
//using System.Threading;
//using System.Threading.Tasks;
//using Filter = MarketViewer.Contracts.Models.Scan.Filter;

//namespace MarketViewer.Application.Handlers.Tools;

//public class ToolsScanHandler(IAmazonS3 s3, IMemoryCache memoryCache, ScanFilterFactoryV2 scanFitlerFactory)
//{
//    private const int MINIMUM_REQUIRED_CANDLES = 30;
//    private const int CANDLES_TO_TAKE = 120;

//    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
//    {
//        PropertyNameCaseInsensitive = true
//    };

//    public async Task<OperationResult<ToolsScanResponse>> Handle(ToolsScanRequest request, CancellationToken cancellationToken)
//    {
//        var timespans = ScanUtilities.GetTimespans(request.Argument);

//        var liveStocksResponses = await GetStocksResponses(isLive: true, request, timespans);
//        var backtestStocksResponses = await GetStocksResponses(isLive: false, request, timespans);

//        var response = new ToolsScanResponse
//        {
//            Ticker = request.Ticker,
//            Timestamp = request.Timestamp
//        };

//        var liveResult = GetScanResult(request, liveStocksResponses);

//        response.Live = liveResult;
//        response.Live.StocksResponses = liveStocksResponses;
//        var asdf = response.Live.StocksResponses[Timespan.minute].Results.Where(q => q.Timestamp == request.Timestamp.AddMinutes(-30).ToUnixTimeMilliseconds()).First().Volume;
//        var qwer = response.Live.StocksResponses[Timespan.hour].Results.Where(q => q.Timestamp == request.Timestamp.AddMinutes(-30).ToUnixTimeMilliseconds()).First().Volume;
//        var backtestResult = GetScanResult(request, backtestStocksResponses);

//        response.Backtest = backtestResult;
//        response.Backtest.StocksResponses = backtestStocksResponses;
//        var zcxv = response.Backtest.StocksResponses[Timespan.minute].Results.Where(q => q.Timestamp == request.Timestamp.AddMinutes(-30).ToUnixTimeMilliseconds()).First().Volume;
//        var q = response.Backtest.StocksResponses[Timespan.hour].Results.Where(q => q.Timestamp == request.Timestamp.AddMinutes(-30).ToUnixTimeMilliseconds()).First().Volume;
//        return new OperationResult<ToolsScanResponse>
//        {
//            Data = response
//        };
//    }

//    private ToolsScanResult GetScanResult(ToolsScanRequest request, Dictionary<Timespan, StocksResponse> responses)
//    {
//        var result = new ToolsScanResult
//        {
//            PassedFilters = [],
//            FailedFilters = []
//        };

//        var sortedFitlers = request.Argument.Filters.OrderByDescending(filter => filter.FirstOperand.GetPriority()).ToList();
//        for (int i = 0; i < sortedFitlers.Count; i++)
//        {
//            var filter = sortedFitlers[i];

//            bool hasTimeframe = filter.FirstOperand.HasTimeframe(out var timeframe);
//            var stocksResponse = hasTimeframe ? responses[timeframe.Timespan] : responses[Timespan.minute];
//            var item = ApplyFilterToStocksResponse(sortedFitlers[i], request.Timestamp, stocksResponse);

//            if (item is null)
//            {
//                result.FailedFilters.Add(filter);
//            }
//            else
//            {
//                result.PassedFilters.Add(filter);
//            }
//        }

//        result.IsSuccess = result.FailedFilters.Count() == 0;

//        return result;
//    }

//    private async Task<Dictionary<Timespan, StocksResponse>> GetStocksResponses(bool isLive, ToolsScanRequest request, IEnumerable<Timespan> timespans)
//    {
//        var stocksResponses = new Dictionary<Timespan, StocksResponse>();

//        foreach (var timespan in timespans.Order())
//        {
            
//            switch (timespan)
//            {
//                case Timespan.minute:
//                    {
//                        var key = isLive ? $"{request.Timestamp:yyyy-MM-dd}-m-stocks.json" : $"backtest/{request.Timestamp:yyyy/MM/dd}/aggregate_1_minute";
//                        var response = isLive ? await GetStocksResponseFromFile(key, request.Ticker) : await GetStocksResponseFromS3(key, request.Ticker);

//                        var adjustedCandles = response.Results.Where(q => q.Timestamp <= request.Timestamp.ToUnixTimeMilliseconds()).ToList();
//                        response.Results = adjustedCandles;

//                        var asdf = response.Results.Where(q => q.Timestamp == request.Timestamp.AddMinutes(-30).ToUnixTimeMilliseconds());

//                        stocksResponses.Add(Timespan.minute, response);
//                        break;
//                    }

//                case Timespan.hour:
//                    {
//                        var key = isLive ? $"{request.Timestamp:yyyy-MM-dd}-h-stocks.json" : $"backtest/{request.Timestamp:yyyy/MM}/aggregate_1_hour";
//                        var response = isLive ? await GetStocksResponseFromFile(key, request.Ticker) : await GetStocksResponseFromS3(key, request.Ticker);
                        
//                        var start = new DateTimeOffset(request.Timestamp.Year, request.Timestamp.Month, request.Timestamp.Day, request.Timestamp.Hour, 0, 0, request.Timestamp.Offset);
//                        var minuteCandles = stocksResponses[Timespan.minute].Results.Where(q => q.Timestamp >= start.ToUnixTimeMilliseconds() && q.Timestamp <= request.Timestamp.AddMinutes(3).ToUnixTimeMilliseconds());

//                        var hourCandle = minuteCandles.First().Clone();
//                        foreach (var candle in minuteCandles)
//                        {
//                            if (hourCandle.High < candle.High)
//                            {
//                                hourCandle.High = candle.High;
//                            }

//                            if (hourCandle.Low > candle.Low)
//                            {
//                                hourCandle.Low = candle.Low;
//                            }


//                            hourCandle.Close = candle.Close;

//                            // TODO: How to do a more precise VWAP?
//                            hourCandle.Vwap = (hourCandle.Close + hourCandle.High + hourCandle.Low) / 3;

//                            hourCandle.Volume += candle.Volume;
//                            hourCandle.TransactionCount += candle.TransactionCount;
//                        }

//                        var hourCandles = response.Results.Where(q => q.Timestamp < start.ToUnixTimeMilliseconds()).ToList();
//                        hourCandles.Add(hourCandle);
//                        response.Results = hourCandles;

//                        stocksResponses.Add(Timespan.hour, response);
//                        break;
//                    }

//                default:
//                    break;
//            }
//        }

//        return stocksResponses;
//    }

//    private async Task<StocksResponse> GetStocksResponseFromFile(string key, string ticker)
//    {
//        try
//        {
//            var cachedStocksResponses = memoryCache.Get<List<StocksResponse>>(key);
//            if (cachedStocksResponses is not null)
//            {
//                var stocksResponse = cachedStocksResponses.FirstOrDefault(q => q.Ticker == ticker);
//                return stocksResponse;
//            }
//            else
//            {
//                var path = Path.Combine(Directory.GetCurrentDirectory(), "Data", key);
//                var json = await File.ReadAllTextAsync(path);
//                var stocksResponses = JsonSerializer.Deserialize<List<StocksResponse>>(json, _jsonSerializerOptions);
//                memoryCache.Set(key, stocksResponses);
//                var stocksResponse = stocksResponses.FirstOrDefault(q => q.Ticker == ticker);
//                return stocksResponse;
//            }
//        }
//        catch (Exception e)
//        {
//            return null;
//        }
//    }

//    private async Task<StocksResponse> GetStocksResponseFromS3(string key, string ticker)
//    {
//        try
//        {
//            var cachedStocksResponses = memoryCache.Get<List<StocksResponse>>(key);
//            if (cachedStocksResponses is not null)
//            {
//                var stocksResponse = cachedStocksResponses.FirstOrDefault(q => q.Ticker == ticker);
//                return stocksResponse;
//            }
//            else
//            {
//                using var s3Response = await s3.GetObjectAsync(new GetObjectRequest
//                {
//                    BucketName = "lad-dev-marketviewer",
//                    Key = key
//                });
//                using var reader = new StreamReader(s3Response.ResponseStream);
//                var json = await reader.ReadToEndAsync();
//                var stocksResponses = JsonSerializer.Deserialize<List<StocksResponse>>(json, _jsonSerializerOptions);
//                memoryCache.Set(key, stocksResponses);
//                var stocksResponse = stocksResponses.FirstOrDefault(q => q.Ticker == ticker);
//                return stocksResponse;
//            }
//        }
//        catch (Exception e)
//        {
//            return null;
//        }
//    }

//    public ScanResponse.Item ApplyFilterToStocksResponse(Filter filter, DateTimeOffset timestamp, StocksResponse stocksResponse, int candlesToTake = CANDLES_TO_TAKE)
//    {
//        bool passesFilter = false;

//        var reducedStocksResponse = new StocksResponse
//        {
//            Ticker = stocksResponse.Ticker,
//            TickerInfo = stocksResponse.TickerInfo,
//            Results = stocksResponse.Results.Where(candle => candle.Timestamp <= timestamp.ToUnixTimeMilliseconds()).TakeLast(candlesToTake).ToList()
//        };

//        if (reducedStocksResponse.Results is null || reducedStocksResponse.Results.Count < MINIMUM_REQUIRED_CANDLES)
//        {
//            return null;
//        }


//        var candles = reducedStocksResponse.Results.Where(q => q.Timestamp >= timestamp.AddMinutes(-30).ToUnixTimeMilliseconds() && q.Timestamp <= timestamp.ToUnixTimeMilliseconds());
//        var volume = candles.Sum(q => q.Volume);

//        var firstFilter = scanFitlerFactory.GetScanFilter(filter.FirstOperand);
//        var firstOperandResult = firstFilter.Compute(filter.FirstOperand, reducedStocksResponse, filter.Timeframe);

//        var secondFilter = scanFitlerFactory.GetScanFilter(filter.SecondOperand);
//        var secondOperandResult = secondFilter.Compute(filter.SecondOperand, reducedStocksResponse, filter.Timeframe);

//        if (firstOperandResult.Length == 0 || secondOperandResult.Length == 0)
//        {
//            return null;
//        }

//        if (filter.Timeframe is not null)
//        {
//            if (reducedStocksResponse.Results.Count < filter.Timeframe.Multiplier)
//            {
//                return null;
//            }
//        }

//        if (filter.CollectionModifier is null)
//        {
//            passesFilter = filter.Operator switch
//            {
//                FilterOperator.lt => firstOperandResult.First() < secondOperandResult.First(),
//                FilterOperator.le => firstOperandResult.First() <= secondOperandResult.First(),
//                FilterOperator.eq => firstOperandResult.First() == secondOperandResult.First(),
//                FilterOperator.ge => firstOperandResult.First() >= secondOperandResult.First(),
//                FilterOperator.gt => firstOperandResult.First() > secondOperandResult.First(),
//                _ => throw new NotImplementedException(),
//            };
//        }
//        else
//        {
//            passesFilter = filter.CollectionModifier.ToLowerInvariant() switch
//            {
//                "all" => filter.Operator switch
//                {
//                    FilterOperator.lt => firstOperandResult.Zip(secondOperandResult, (x, y) => x < y).All(result => result == true),
//                    FilterOperator.le => firstOperandResult.Zip(secondOperandResult, (x, y) => x <= y).All(result => result == true),
//                    FilterOperator.eq => firstOperandResult.Zip(secondOperandResult, (x, y) => x == y).All(result => result == true),
//                    FilterOperator.ge => firstOperandResult.Zip(secondOperandResult, (x, y) => x >= y).All(result => result == true),
//                    FilterOperator.gt => firstOperandResult.Zip(secondOperandResult, (x, y) => x > y).All(result => result == true),
//                    _ => throw new NotImplementedException(),
//                },
//                "any" => filter.Operator switch
//                {
//                    FilterOperator.lt => firstOperandResult.Zip(secondOperandResult, (x, y) => x < y).Any(result => result == true),
//                    FilterOperator.le => firstOperandResult.Zip(secondOperandResult, (x, y) => x <= y).Any(result => result == true),
//                    FilterOperator.eq => firstOperandResult.Zip(secondOperandResult, (x, y) => x == y).Any(result => result == true),
//                    FilterOperator.ge => firstOperandResult.Zip(secondOperandResult, (x, y) => x >= y).Any(result => result == true),
//                    FilterOperator.gt => firstOperandResult.Zip(secondOperandResult, (x, y) => x > y).Any(result => result == true),
//                    _ => throw new NotImplementedException(),
//                },
//                "average" => filter.Operator switch
//                {
//                    FilterOperator.lt => firstOperandResult.Average() < secondOperandResult.Average(),
//                    FilterOperator.le => firstOperandResult.Average() <= secondOperandResult.Average(),
//                    FilterOperator.eq => firstOperandResult.Average() == secondOperandResult.Average(),
//                    FilterOperator.ge => firstOperandResult.Average() >= secondOperandResult.Average(),
//                    FilterOperator.gt => firstOperandResult.Average() > secondOperandResult.Average(),
//                    _ => throw new NotImplementedException(),
//                },
//                _ => throw new NotImplementedException()
//            };
//        }

//        if (!passesFilter)
//        {
//            return null;
//        }

//        return new ScanResponse.Item
//        {
//            Ticker = stocksResponse.Ticker,
//            Price = stocksResponse.Results.Last().Close,
//            Float = stocksResponse.TickerInfo?.TickerDetails?.Float
//        };
//    }
//}
