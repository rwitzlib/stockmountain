using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Massive.Client.UnitTests;

internal static class TestClientFactory
{
    public static MassiveClient Create(Mock<HttpMessageHandler> handler)
    {
        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri(MassiveClient.DefaultBaseUrl)
        };

        return new MassiveClient(httpClient, NullLogger<MassiveClient>.Instance);
    }
}
