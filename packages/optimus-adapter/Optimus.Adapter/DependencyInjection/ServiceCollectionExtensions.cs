using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Optimus.Adapter.DependencyInjection;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterAdapters(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<AdapterFactory>()
            .AddSingleton<DefaultAdapter>()
            .AddSingleton<SchwabAdapter>();

        services.AddHttpClient<DefaultAdapter>(client =>
        {
            client.BaseAddress = new Uri(configuration.GetSection("Urls").GetValue<string>("MarketViewer"));
            client.Timeout = TimeSpan.FromSeconds(600);
        });

        return services;
    }
}
