using MarketViewer.Api.HostedServices;
using MarketViewer.Contracts.Caching;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MarketViewer.Api.Healthcheck;

/// <summary>
/// Readiness: reports whether the market cache actually holds data. Warming or
/// failed warmups report Unhealthy (503 on /health/ready) so deploys and monitors
/// can tell "up" from "up with data". A cache that is populated but hasn't been
/// re-warmed within the expected daily window reports Degraded — still serving,
/// but stale.
/// </summary>
public class CacheReadinessHealthCheck(CacheWarmupState warmupState, IMarketCache marketCache) : IHealthCheck
{
    /// <summary>Daily re-warm runs at 3:30am ET; anything older than this missed at least one.</summary>
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(30);

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!warmupState.IsReady || marketCache.GetTickers()?.Any() != true)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Market cache not ready. Status: {warmupState.Status}. Last error: {warmupState.LastError ?? "none"}"));
        }

        var age = DateTimeOffset.Now - warmupState.LastSuccessAt;
        if (age > MaxAge)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Market cache is stale. Last successful warmup: {warmupState.LastSuccessAt}. Last error: {warmupState.LastError ?? "none"}"));
        }

        return Task.FromResult(HealthCheckResult.Healthy($"Market cache ready. Last warmup: {warmupState.LastSuccessAt}"));
    }
}
