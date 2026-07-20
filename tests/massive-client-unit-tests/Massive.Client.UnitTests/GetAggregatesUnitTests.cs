using System.Net;
using System.Text.Json;
using FluentAssertions;
using Massive.Client.Models;
using Massive.Client.Requests;
using Massive.Client.Responses;
using Moq;
using Moq.Protected;

namespace Massive.Client.UnitTests;

public class GetAggregatesUnitTests
{
    public static TheoryData<MassiveAggregateRequest> InvalidRequests => new()
    {
        new() { Ticker = null, Multiplier = 1, Timespan = "minute", From = "2024-03-25", To = "2024-03-26" },
        new() { Ticker = "SPY", Multiplier = 1, Timespan = null, From = "2024-03-25", To = "2024-03-26" },
        new() { Ticker = "SPY", Multiplier = 1, Timespan = "minute", From = null, To = "2024-03-26" },
        new() { Ticker = "SPY", Multiplier = 1, Timespan = "minute", From = "2024-03-25", To = null },
        new() { Ticker = "SPY", Multiplier = 0, Timespan = "minute", From = "2024-03-25", To = "2024-03-26" },
        new() { Ticker = "SPY", Multiplier = 1, Timespan = "minute", From = "2024-03-26", To = "2024-03-25" }
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task GetAggregates_With_Invalid_Request_Returns_BadRequest(MassiveAggregateRequest request)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var response = await TestClientFactory.Create(handler).GetAggregates(request);

        response.Status.Should().Be(HttpStatusCode.BadRequest.ToString());
        response.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAggregates_With_Invalid_Json_Returns_InternalServerError()
    {
        var handler = Handler(HttpStatusCode.OK, "invalid json");

        var response = await TestClientFactory.Create(handler).GetAggregates(ValidRequest());

        response.Status.Should().Be(HttpStatusCode.InternalServerError.ToString());
        response.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAggregates_Without_NextUrl_Returns_Single_Response()
    {
        var handler = Handler(HttpStatusCode.OK, JsonSerializer.Serialize(AggregateResponse(2, null)));

        var response = await TestClientFactory.Create(handler).GetAggregates(ValidRequest());

        response.Results.Should().HaveCount(2);
        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetAggregates_With_Multiple_NextUrl_Pages_Fetches_All_Results()
    {
        var responses = new[]
        {
            AggregateResponse(2, "https://api.massive.com/v2/aggs?cursor=page1"),
            AggregateResponse(2, "https://api.massive.com/v2/aggs?cursor=page2", 1711324920000),
            AggregateResponse(1, null, 1711325040000)
        };
        var handler = SequenceHandler(responses);

        var response = await TestClientFactory.Create(handler).GetAggregates(ValidRequest());

        response.Results.Should().HaveCount(5);
        handler.Protected().Verify(
            "SendAsync",
            Times.Exactly(3),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetAggregates_When_Next_Page_Fails_Returns_Original_Results()
    {
        var firstResponse = AggregateResponse(2, "https://api.massive.com/v2/aggs?cursor=next");
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(firstResponse))
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var response = await TestClientFactory.Create(handler).GetAggregates(ValidRequest());

        response.Results.Should().HaveCount(2);
        response.Status.Should().Be(HttpStatusCode.OK.ToString());
    }

    private static MassiveAggregateRequest ValidRequest() => new()
    {
        Ticker = "SPY",
        Multiplier = 1,
        Timespan = "minute",
        From = "2024-03-25",
        To = "2024-03-26"
    };

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

    private static Mock<HttpMessageHandler> SequenceHandler(IReadOnlyList<MassiveAggregateResponse> responses)
    {
        var handler = new Mock<HttpMessageHandler>();
        var callCount = 0;
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(responses[callCount++]))
            });
        return handler;
    }

    private static MassiveAggregateResponse AggregateResponse(int count, string? nextUrl, long startTimestamp = 1711324800000)
    {
        return new MassiveAggregateResponse
        {
            Ticker = "SPY",
            Status = HttpStatusCode.OK.ToString(),
            ResultsCount = count,
            Results = Enumerable.Range(0, count)
                .Select(index => new Bar { Timestamp = startTimestamp + (index * 60000) })
                .ToList(),
            NextUrl = nextUrl
        };
    }
}
