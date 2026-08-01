using MarketViewer.Contracts.Enums.Strategy;
using Microsoft.Extensions.DependencyInjection;
using Optimus.Adapter.Interfaces;
using System.Diagnostics.CodeAnalysis;

namespace Optimus.Adapter;

[ExcludeFromCodeCoverage]
public class AdapterFactory(IServiceProvider serviceProvider)
{
    public IAdapter GetAdaptor(IntegrationType integrationType)
    {
        return integrationType switch
        {
            IntegrationType.Default => serviceProvider.GetService<DefaultAdapter>(),
            IntegrationType.Schwab => serviceProvider.GetService<SchwabAdapter>(),
            IntegrationType.AlpacaPaper => serviceProvider.GetRequiredKeyedService<AlpacaAdapter>(DependencyInjection.ServiceCollectionExtensions.AlpacaPaperKey),
            IntegrationType.AlpacaLive => serviceProvider.GetRequiredKeyedService<AlpacaAdapter>(DependencyInjection.ServiceCollectionExtensions.AlpacaLiveKey),
            // IntegrationType.Fidelity => serviceProvider.GetService<FidelityAdaptor>(),
            // IntegrationType.ETrade => serviceProvider.GetService<ETradeAdaptor>(),
            _ => serviceProvider.GetService<DefaultAdapter>()
        };
    }
}
