using Alpaca.Client.Config;
using Alpaca.Client.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Alpaca.Client.DependencyInjection;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterAlpacaClients(this IServiceCollection services, IConfiguration configuration)
    {
        var config = configuration.GetSection("Alpaca").Get<AlpacaConfig>() ?? new AlpacaConfig();
        config.ApiKeyId = Environment.GetEnvironmentVariable("ALPACA_API_KEY_ID") ?? config.ApiKeyId;
        config.ApiSecretKey = Environment.GetEnvironmentVariable("ALPACA_API_SECRET_KEY") ?? config.ApiSecretKey;

        services.AddSingleton(config);

        services.AddHttpClient(AlpacaTradingClient.HttpClientName, client =>
        {
            client.BaseAddress = new Uri(config.BaseUrl);
            client.DefaultRequestHeaders.Add("APCA-API-KEY-ID", config.ApiKeyId);
            client.DefaultRequestHeaders.Add("APCA-API-SECRET-KEY", config.ApiSecretKey);
        });

        services.AddSingleton<IAlpacaTradingClient, AlpacaTradingClient>()
            .AddSingleton<MarketCalendarService>();

        return services;
    }
}
