using Microsoft.Extensions.DependencyInjection;
using SchwabApi.Clients;
using SchwabApi.Interfaces;

namespace SchwabApi.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterSchwabClients(this IServiceCollection services)
    {        
        services.AddHttpClient("authentication", client =>
        {
            client.BaseAddress = new Uri("https://api.schwabapi.com/v1/oauth/");
        });

        services.AddHttpClient("trader", client =>
        {
            client.BaseAddress = new Uri("https://api.schwabapi.com/trader/v1/");
        });

        services.AddSingleton<IAuthenticationClient, AuthenticationClient>()
            .AddSingleton<ITraderClient, TraderClient>();

        return services;
    }
}
