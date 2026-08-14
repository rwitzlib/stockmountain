using MarketViewer.Clients.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;

namespace MarketViewer.Clients.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the live-price client against the MarketViewer API. The internal token
    /// authenticates via the API's shared-secret InternalToken policy; when it is empty
    /// the client still registers but every call will be rejected (and callers fall back
    /// to their snapshot path).
    /// </summary>
    public static IServiceCollection AddLivePriceClient(this IServiceCollection services, string baseUrl, string internalToken)
    {
        // Fail at startup, not on the first exit-evaluation tick.
        ArgumentException.ThrowIfNullOrEmpty(baseUrl);

        services.AddHttpClient<ILivePriceClient, LivePriceClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            // Exit evaluation runs every 10 seconds; a hung request must not eat the tick.
            client.Timeout = TimeSpan.FromSeconds(5);

            if (!string.IsNullOrEmpty(internalToken))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", internalToken);
            }
        });

        return services;
    }
}
