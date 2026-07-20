using System.Net;
using System.Text.Json;
using FluentAssertions;
using Massive.Client.Models;
using Massive.Client.Requests;
using Massive.Client.Responses;
using Moq;
using Moq.Protected;

namespace Massive.Client.UnitTests;

public class GetTickersUnitTests
{
    [Fact]
    public async Task GetTickers_With_BadRequest_Returns_BadRequest()
    {
        var handler = Handler(HttpStatusCode.BadRequest, "bad request");

        var response = await TestClientFactory.Create(handler).GetTickers(new MassiveGetTickersRequest());

        response.Status.Should().Be(HttpStatusCode.BadRequest.ToString());
        response.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTickers_After_Partial_Page_Failure_Returns_Collected_Results()
    {
        var firstPage = new MassiveGetTickersResponse
        {
            Status = "OK",
            Results = [new TickerDetails { Ticker = "SPY" }],
            NextUrl = "https://api.massive.com/v3/reference/tickers?cursor=next"
        };
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(firstPage))
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest));

        var response = await TestClientFactory.Create(handler).GetTickers(new MassiveGetTickersRequest());

        response.Status.Should().Be(HttpStatusCode.OK.ToString());
        response.Results.Should().ContainSingle().Which.Ticker.Should().Be("SPY");
    }

    [Fact]
    public async Task GetTickers_With_Invalid_Json_Returns_InternalServerError()
    {
        var handler = Handler(HttpStatusCode.OK, "invalid json");

        var response = await TestClientFactory.Create(handler).GetTickers(new MassiveGetTickersRequest());

        response.Status.Should().Be(HttpStatusCode.InternalServerError.ToString());
        response.Results.Should().BeEmpty();
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
