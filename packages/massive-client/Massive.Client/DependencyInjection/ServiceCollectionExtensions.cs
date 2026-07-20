using System.Net.Http.Headers;
using Massive.Client.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Massive.Client.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMassiveClient(
        this IServiceCollection services,
        string apiKey,
        string baseUrl = MassiveClient.DefaultBaseUrl)
    {
        services.AddHttpClient<IMassiveClient, MassiveClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(
                apiKey.StartsWith("Bearer ") ? apiKey : $"Bearer {apiKey}");
        });

        return services;
    }
}
