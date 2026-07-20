using FluentAssertions;
using Massive.Client.DependencyInjection;
using Massive.Client.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Massive.Client.UnitTests;

public class MassiveClientUnitTests
{
    [Fact]
    public void DefaultBaseUrl_Uses_Massive_Api()
    {
        MassiveClient.DefaultBaseUrl.Should().Be("https://api.massive.com");
    }

    [Fact]
    public void AddMassiveClient_Registers_IMassiveClient()
    {
        using var serviceProvider = new ServiceCollection()
            .AddMassiveClient("test-token")
            .AddLogging()
            .BuildServiceProvider();

        var client = serviceProvider.GetRequiredService<IMassiveClient>();

        client.Should().BeOfType<MassiveClient>();
    }
}
