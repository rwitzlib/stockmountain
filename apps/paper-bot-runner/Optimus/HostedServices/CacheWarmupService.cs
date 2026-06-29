using MarketViewer.Contracts.Caching;
using Optimus.Infrastructure.Repositories;

namespace Optimus.HostedServices;

public class CacheWarmupService(
    TickerCache tickerCache,
    MetaRepository metaRepository,
    ILogger<CacheWarmupService> logger) : IHostedLifecycleService
{
    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting ticker cache warmup...");

            var meta = await metaRepository.GetUniverseMeta();

            var snapshot = await metaRepository.GetUniverseMetaSnapshot(meta);

            if (snapshot is null)
            {
                logger.LogWarning("Failed to load universe snapshot - ticker cache will be empty");
                return;
            }

            var maxId = snapshot.MaxId;
            var symbols = snapshot.Symbols;

            tickerCache.IdToSymbol = new string[maxId + 1];

            for (int i = 0; i < symbols.Length && i <= maxId; i++)
            {
                var symbol = symbols[i];
                if (!string.IsNullOrWhiteSpace(symbol))
                {
                    tickerCache.IdToSymbol[i] = symbol;
                    tickerCache.SymbolToId[symbol] = i;
                }
            }

            logger.LogInformation("Ticker cache warmup complete. Loaded {Count} symbols.", tickerCache.SymbolToId.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during ticker cache warmup: {Message}", ex.Message);
        }
    }

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
