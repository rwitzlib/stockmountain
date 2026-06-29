using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MarketViewer.Api.Healthcheck
{
    public class PingHealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(HealthCheckResult.Healthy());
        }
    }
}
