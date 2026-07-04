using Amazon.Lambda.Core;
using Backtest.Lambda.Services;
using MarketViewer.Contracts.Requests.Market.Backtest;
using MarketViewer.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Backtest.Lambda;

/// <summary>
/// This lambda can be ignored for now
/// </summary>
/// <param name="serviceProvider"></param>
public class FilterFunction(IServiceProvider serviceProvider)
{
    private readonly IMemoryCache _memoryCache = serviceProvider.GetService<IMemoryCache>();
    public readonly ScannerService _scannerService = serviceProvider.GetService<ScannerService>();
    public readonly IndicatorExpressionEngine _engine = serviceProvider.GetService<IndicatorExpressionEngine>();
    private readonly ILogger<WorkerFunction> _logger = serviceProvider.GetService<ILogger<WorkerFunction>>();

    private readonly float MEMORY_FACTOR = Backtest.Lambda.Utilities.LambdaEnvironment.GetMemoryFactor();

    public FilterFunction() : this(Startup.ConfigureServices()) { }

    public async Task FunctionHandler(WorkerRequest request, ILambdaContext context)
    {
        var sp = new Stopwatch();
        sp.Start();

        try
        {
            _logger.LogInformation("Starting backtest worker for {date}", request.Date.ToString("yyyy-MM-dd"));

            var strategyEntries = await _scannerService.GetStrategyEntries(request);

            _logger.LogInformation("Found {EntryCount} entries in {ElapsedSeconds} seconds", strategyEntries.Count, sp.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: {message}", ex.Message);
            _logger.LogError("Stacktrace: {stackTrace}", ex.StackTrace);
        }
        finally
        {
            if (_memoryCache is MemoryCache)
            {
                _logger.LogInformation("Clearing memory cache");
                GC.Collect();
            }
        }
    }
}
