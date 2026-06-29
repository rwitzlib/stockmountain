using MarketViewer.Core.Auth;
using MarketViewer.Core.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Core.DependencyInjection;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AuthContext>()
            .AddSingleton<MarketMetrics>();

        return services;
    }
}
