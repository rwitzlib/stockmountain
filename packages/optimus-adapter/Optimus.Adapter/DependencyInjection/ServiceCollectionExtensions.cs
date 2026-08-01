using Alpaca.Client.Interfaces;
using MarketViewer.Contracts.Enums.Strategy;
using Massive.Client.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Optimus.Adapter.Config;
using Optimus.Infrastructure.Repositories;
using System.Diagnostics.CodeAnalysis;
using AlpacaDI = Alpaca.Client.DependencyInjection.ServiceCollectionExtensions;

namespace Optimus.Adapter.DependencyInjection;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public const string AlpacaPaperKey = "alpaca-adapter-paper";
    public const string AlpacaLiveKey = "alpaca-adapter-live";

    /// <summary>
    /// Registers all broker adapters. Requires RegisterAlpacaClients to also be called —
    /// the Alpaca adapters resolve the keyed paper/live trading clients from there.
    /// </summary>
    public static IServiceCollection RegisterAdapters(this IServiceCollection services, IConfiguration configuration)
    {
        var alpacaAdapterConfig = configuration.GetSection("AlpacaAdapter").Get<AlpacaAdapterConfig>() ?? new AlpacaAdapterConfig();
        services.AddSingleton(alpacaAdapterConfig);

        services.AddSingleton<AdapterFactory>()
            .AddSingleton<DefaultAdapter>()
            .AddSingleton<SchwabAdapter>();

        services.AddKeyedSingleton(AlpacaPaperKey, (sp, _) => BuildAlpacaAdapter(sp, AlpacaDI.PaperClientKey, TradeType.Paper));
        services.AddKeyedSingleton(AlpacaLiveKey, (sp, _) => BuildAlpacaAdapter(sp, AlpacaDI.LiveClientKey, TradeType.Live));

        return services;
    }

    private static AlpacaAdapter BuildAlpacaAdapter(IServiceProvider sp, string clientKey, TradeType tradeType)
    {
        return new AlpacaAdapter(
            sp.GetRequiredKeyedService<IAlpacaTradingClient>(clientKey),
            tradeType,
            sp.GetRequiredService<TradeRepository>(),
            sp.GetRequiredService<IMassiveClient>(),
            sp.GetRequiredService<AlpacaAdapterConfig>(),
            sp.GetRequiredService<ILogger<AlpacaAdapter>>());
    }
}
