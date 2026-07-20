using System.Net;
using FluentAssertions;
using Moq;
using Moq.Protected;

namespace Massive.Client.UnitTests;

public class GetDailyMarketSummaryUnitTests
{
    [Fact]
    public async Task GetDailyMarketSummary_With_Invalid_Json_Returns_InternalServerError()
    {
        var handler = Handler(HttpStatusCode.OK, "invalid json");

        var response = await TestClientFactory.Create(handler).GetDailyMarketSummary();

        response.Status.Should().Be(HttpStatusCode.InternalServerError.ToString());
        response.Results.Should().BeEmpty();
        response.QueryCount.Should().Be(0);
        response.ResultsCount.Should().Be(0);
        response.Count.Should().Be(0);
    }

    [Fact]
    public async Task GetDailyMarketSummary_With_BadRequest_Returns_BadRequest()
    {
        var handler = Handler(HttpStatusCode.BadRequest, string.Empty);

        var response = await TestClientFactory.Create(handler).GetDailyMarketSummary();

        response.Status.Should().Be(HttpStatusCode.BadRequest.ToString());
        response.Results.Should().BeEmpty();
        response.QueryCount.Should().Be(0);
        response.ResultsCount.Should().Be(0);
        response.Count.Should().Be(0);
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
