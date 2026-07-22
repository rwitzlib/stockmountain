using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Optimus.Adapter.DependencyInjection;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterAdapters(this IServiceCollection services)
    {
        services.AddSingleton<AdapterFactory>()
            .AddSingleton<DefaultAdapter>()
            .AddSingleton<SchwabAdapter>();

        return services;
    }
}
