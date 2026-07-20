using System.Net;
using FluentAssertions;
using Moq;
using Moq.Protected;

namespace Massive.Client.UnitTests;

public class GetTickerDetailsUnitTests
{
    [Fact]
    public async Task GetTickerDetails_With_Null_Ticker_Returns_BadRequest()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        var response = await TestClientFactory.Create(handler).GetTickerDetails(null!);

        response.Status.Should().Be(HttpStatusCode.BadRequest.ToString());
        response.TickerDetails.Should().BeNull();
    }

    [Fact]
    public async Task GetTickerDetails_With_NotFound_Response_Returns_NotFound()
    {
        var handler = Handler(HttpStatusCode.NotFound, string.Empty);

        var response = await TestClientFactory.Create(handler).GetTickerDetails("UNKNOWN");

        response.Status.Should().Be(HttpStatusCode.NotFound.ToString());
        response.TickerDetails.Should().BeNull();
    }

    [Fact]
    public async Task GetTickerDetails_With_Invalid_Json_Returns_InternalServerError()
    {
        var handler = Handler(HttpStatusCode.OK, "invalid json");

        var response = await TestClientFactory.Create(handler).GetTickerDetails("SPY");

        response.Status.Should().Be(HttpStatusCode.InternalServerError.ToString());
        response.TickerDetails.Should().BeNull();
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
