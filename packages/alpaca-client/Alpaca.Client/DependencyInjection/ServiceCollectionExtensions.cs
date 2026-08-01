using Alpaca.Client.Config;
using Alpaca.Client.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Alpaca.Client.DependencyInjection;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    /// <summary>DI key for the paper-environment trading client.</summary>
    public const string PaperClientKey = "alpaca-paper";

    /// <summary>DI key for the live-environment trading client.</summary>
    public const string LiveClientKey = "alpaca-live";

    public static IServiceCollection RegisterAlpacaClients(this IServiceCollection services, IConfiguration configuration)
    {
        var config = configuration.GetSection("Alpaca").Get<AlpacaConfig>() ?? new AlpacaConfig();
        config.ApiKeyId = Environment.GetEnvironmentVariable("ALPACA_API_KEY_ID") ?? config.ApiKeyId;
        config.ApiSecretKey = Environment.GetEnvironmentVariable("ALPACA_API_SECRET_KEY") ?? config.ApiSecretKey;
        config.LiveApiKeyId = Environment.GetEnvironmentVariable("ALPACA_LIVE_API_KEY_ID") ?? config.LiveApiKeyId;
        config.LiveApiSecretKey = Environment.GetEnvironmentVariable("ALPACA_LIVE_API_SECRET_KEY") ?? config.LiveApiSecretKey;

        services.AddSingleton(config);

        services.AddHttpClient(AlpacaTradingClient.HttpClientName, client =>
        {
            client.BaseAddress = new Uri(config.BaseUrl);
            client.DefaultRequestHeaders.Add("APCA-API-KEY-ID", config.ApiKeyId);
            client.DefaultRequestHeaders.Add("APCA-API-SECRET-KEY", config.ApiSecretKey);
        });

        services.AddHttpClient(AlpacaTradingClient.LiveHttpClientName, client =>
        {
            client.BaseAddress = new Uri(config.LiveBaseUrl);
            client.DefaultRequestHeaders.Add("APCA-API-KEY-ID", config.LiveApiKeyId);
            client.DefaultRequestHeaders.Add("APCA-API-SECRET-KEY", config.LiveApiSecretKey);
        });

        services.AddKeyedSingleton<IAlpacaTradingClient>(PaperClientKey, (sp, _) => new AlpacaTradingClient(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<ILogger<AlpacaTradingClient>>(),
            AlpacaTradingClient.HttpClientName));

        services.AddKeyedSingleton<IAlpacaTradingClient>(LiveClientKey, (sp, _) => new AlpacaTradingClient(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<ILogger<AlpacaTradingClient>>(),
            AlpacaTradingClient.LiveHttpClientName));

        // Unkeyed resolution stays on paper: clock/calendar are identical in both
        // environments, and nothing should reach live implicitly.
        services.AddSingleton(sp => sp.GetRequiredKeyedService<IAlpacaTradingClient>(PaperClientKey))
            .AddSingleton<MarketCalendarService>();

        return services;
    }
}
