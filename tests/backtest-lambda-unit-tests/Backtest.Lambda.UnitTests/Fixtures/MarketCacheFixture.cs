using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Backtest.Lambda.UnitTests.Fixtures;

public class MarketCacheFixture : IDisposable
{
    public MarketCacheFixture()
    {
        MarketCache = ServiceProvider.GetService<IMarketCache>();
    }

    public IServiceProvider ServiceProvider = Startup.ConfigureServices();
    public IMarketCache MarketCache { get; private set; }

    public void Dispose()
    {
        var memoryCache = ServiceProvider.GetService<IMemoryCache>();
        ((MemoryCache)memoryCache).Clear();
    }
}
