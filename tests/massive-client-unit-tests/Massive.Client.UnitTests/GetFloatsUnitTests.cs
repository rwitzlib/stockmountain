using System.Net;
using System.Text.Json;
using FluentAssertions;
using Massive.Client.Models;
using Massive.Client.Responses;
using Moq;
using Moq.Protected;

namespace Massive.Client.UnitTests;

public class GetFloatsUnitTests
{
    [Fact]
    public async Task GetFloats_With_BadRequest_Returns_BadRequest()
    {
        var handler = Handler(HttpStatusCode.BadRequest, "bad request");

        var response = await TestClientFactory.Create(handler).GetFloats();

        response.Status.Should().Be(HttpStatusCode.BadRequest.ToString());
        response.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFloats_With_Valid_Response_Returns_Results()
    {
        var page = new MassiveFloatResponse
        {
            Status = "OK",
            Results =
            [
                new StockFloat
                {
                    Ticker = "AAPL",
                    EffectiveDate = "2025-11-01",
                    FreeFloat = 15_000_000_000,
                    FreeFloatPercent = 98.5
                }
            ]
        };
        var handler = Handler(HttpStatusCode.OK, JsonSerializer.Serialize(page));

        var response = await TestClientFactory.Create(handler).GetFloats("AAPL");

        response.Status.Should().Be(HttpStatusCode.OK.ToString());
        var stockFloat = response.Results.Should().ContainSingle().Subject;
        stockFloat.Ticker.Should().Be("AAPL");
        stockFloat.FreeFloat.Should().Be(15_000_000_000);
        stockFloat.FreeFloatPercent.Should().Be(98.5);
    }

    [Fact]
    public async Task GetFloats_With_Pagination_Returns_All_Results()
    {
        var firstPage = new MassiveFloatResponse
        {
            Status = "OK",
            Results = [new StockFloat { Ticker = "AAPL", FreeFloat = 100 }],
            NextUrl = "https://api.massive.com/stocks/vX/float?cursor=next"
        };
        var secondPage = new MassiveFloatResponse
        {
            Status = "OK",
            Results = [new StockFloat { Ticker = "MSFT", FreeFloat = 200 }]
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
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(secondPage))
            });

        var response = await TestClientFactory.Create(handler).GetFloats();

        response.Status.Should().Be(HttpStatusCode.OK.ToString());
        response.Results.Should().HaveCount(2);
        response.Results.Select(q => q.Ticker).Should().ContainInOrder("AAPL", "MSFT");
    }

    [Fact]
    public async Task GetFloats_After_Partial_Page_Failure_Returns_Collected_Results()
    {
        var firstPage = new MassiveFloatResponse
        {
            Status = "OK",
            Results = [new StockFloat { Ticker = "AAPL", FreeFloat = 100 }],
            NextUrl = "https://api.massive.com/stocks/vX/float?cursor=next"
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

        var response = await TestClientFactory.Create(handler).GetFloats();

        response.Status.Should().Be(HttpStatusCode.OK.ToString());
        response.Results.Should().ContainSingle().Which.Ticker.Should().Be("AAPL");
    }

    [Fact]
    public async Task GetFloats_With_Invalid_Json_Returns_InternalServerError()
    {
        var handler = Handler(HttpStatusCode.OK, "invalid json");

        var response = await TestClientFactory.Create(handler).GetFloats();

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
