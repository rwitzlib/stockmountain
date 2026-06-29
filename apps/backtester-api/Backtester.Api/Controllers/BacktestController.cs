using Backtest.Lambda.Services;
using Backtester.Api.Requests;
using MarketViewer.Contracts.Caching;
using MarketViewer.Filters;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Backtester.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class BacktestController(
        IndicatorExpressionEngine engine,
        IMarketCache marketCache,
        DataCache dataCache,
        ScannerService scannerService,
        ILogger<BacktestController> logger) : ControllerBase
    {
        [HttpPost("{date}")]
        public async Task<IActionResult> Single(DateTimeOffset date, [FromBody] BacktestRequest request)
        {
            var sp = Stopwatch.StartNew();
            try
            {
                logger.LogInformation("Starting backtest on {date} for filter {filter}.", date, request.Filters[0]);

                var timeframe = engine.ExtractTimeframeFromScript(request.Filters[0]);
                var stocksResponses = await marketCache.Initialize(date, timeframe);

                if (stocksResponses is null || !stocksResponses.Any())
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, "Error occurred gathering backtest results.");
                }

                if (!await dataCache.Setup(date, [timeframe]))
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, "Error occurred gathering backtest results.");
                }

                await scannerService.GetResultsFromFilter(request.Filters[0], date);

                sp.Stop();

                return Ok();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred gathering backtest results.");
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
            finally
            {
                logger.LogInformation("Finished backtest in {elapsed} seconds.", sp.Elapsed.TotalSeconds);
            }
        }

        [HttpPost("{start}/{end}")]
        public async Task<IActionResult> Multi(DateTimeOffset start, DateTimeOffset end, [FromBody] BacktestRequest request)
        {
            var sp = Stopwatch.StartNew();
            try
            {
                var dateRange = (end - start).TotalDays;

                for (int i = 0; i <= dateRange; i++)
                {
                    var date = start.AddDays(i);

                    foreach (var filter in request.Filters)
                    {
                        logger.LogInformation("Starting backtest on {date} for filter {filter}.", date, filter);

                        var timeframe = engine.ExtractTimeframeFromScript(filter);
                        var stocksResponses = await marketCache.Initialize(date, timeframe);

                        if (stocksResponses is null || !stocksResponses.Any())
                        {
                            return StatusCode(StatusCodes.Status500InternalServerError, "Error occurred gathering backtest results.");
                        }

                        if (!await dataCache.Setup(date, [timeframe]))
                        {
                            return StatusCode(StatusCodes.Status500InternalServerError, "Error occurred gathering backtest results.");
                        }

                        await scannerService.GetResultsFromFilter(filter, date);
                    }
                }
                sp.Stop();
                return Ok();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred gathering backtest results.");
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
            finally
            {
                logger.LogInformation("Finished backtest in {elapsed} seconds.", sp.Elapsed.TotalSeconds);
            }
        }
    }
}
