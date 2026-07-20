using System.Net;
using FluentAssertions;
using Moq;
using Moq.Protected;

namespace Massive.Client.UnitTests;

public class GetAllTickersSnapshotUnitTests
{
    [Fact]
    public async Task GetAllTickersSnapshot_With_BadRequest_Returns_BadRequest()
    {
        var handler = Handler(HttpStatusCode.BadRequest, string.Empty);

        var response = await TestClientFactory.Create(handler).GetAllTickersSnapshot(null!);

        response.Status.Should().Be(HttpStatusCode.BadRequest.ToString());
        response.Tickers.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllTickersSnapshot_With_Invalid_Json_Returns_InternalServerError()
    {
        var handler = Handler(HttpStatusCode.OK, "invalid json");

        var response = await TestClientFactory.Create(handler).GetAllTickersSnapshot("SPY");

        response.Status.Should().Be(HttpStatusCode.InternalServerError.ToString());
        response.Tickers.Should().BeEmpty();
    }

    private static Mock<HttpMessageHandler> Handler(HttpStatusCode status, string content)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status) { Content = new StringContent(content) });
        return handler;
    }
}
