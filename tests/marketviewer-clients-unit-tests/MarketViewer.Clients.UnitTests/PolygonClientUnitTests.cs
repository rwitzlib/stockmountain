using AutoFixture;
using FluentAssertions;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.PolygonApi;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Xunit;

namespace MarketViewer.Clients.UnitTests
{
    public class PolygonClientUnitTests
    {
        #region Private Fields
        private Fixture _fixture;
        private readonly Mock<HttpMessageHandler> _handler;
        private readonly IPolygonClient _classUnderTest;
        #endregion

        #region Constructor
        public PolygonClientUnitTests()
        {
            _fixture = new Fixture();
            _handler = new Mock<HttpMessageHandler>();

            var client = new HttpClient(_handler.Object)
            {
                BaseAddress = new Uri("https://localhost/")
            };

            _classUnderTest = new PolygonClient(client);
        }
        #endregion

        #region GetTickerDetails
        [Fact]
        public async Task GetTickerDetails_With_OK_Response_Returns_TickerDetails()
        {
            // Arrange
            var ticker = "META";
            var json = GivenValidGetTickerDetailsResponse();

            _handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(json)
                });

            // Act
            var response = await _classUnderTest.GetTickerDetails(ticker);

            // Assert
            response.Should().NotBeNull();
        }

        [Fact]
        public async Task GetTickerDetails_With_BadRequest_Response_Returns_Null()
        {
            // Arrange
            var ticker = "META";
            var json = GivenValidGetTickerDetailsResponse();

            _handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Content = new StringContent(json)
                });

            // Act
            var response = await _classUnderTest.GetTickerDetails(ticker);

            // Assert
            response.Should().BeNull();
        }

        [Fact]
        public async Task GetTickerDetails_With_Null_Ticker_Returns_Null()
        {
            // Arrange
            var json = GivenValidGetTickerDetailsResponse();

            _handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(json)
                });

            // Act
            var response = await _classUnderTest.GetTickerDetails(null);

            // Assert
            response.Should().BeNull();
        }
        #endregion

        #region GetAllTickers
        [Fact]
        public async Task GetAllTickers_With_OK_Response_Returns_TickerDetails()
        {
            // Arrange
            var json = GivenValidPolygonGetTickersResponse();

            _handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(json)
                });

            // Act
            var response = await _classUnderTest.GetAllTickers();

            // Assert
            response.Should().NotBeNull();
            response.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task GetAllTickers_With_BadRequest_Response_Returns_No_TickerDetails()
        {
            // Arrange
            var json = GivenValidPolygonGetTickersResponse();

            _handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Content = new StringContent(json)
                });

            // Act
            var response = await _classUnderTest.GetAllTickers();

            // Assert
            response.Should().NotBeNull();
            response.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAllTickers_With_InternalServerError_Response_Returns_No_TickerDetails()
        {
            // Arrange
            var json = GivenValidPolygonGetTickersResponse();

            _handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Content = new StringContent(json)
                });

            // Act
            var response = await _classUnderTest.GetAllTickers();

            // Assert
            response.Should().NotBeNull();
            response.Should().BeEmpty();
        }
        #endregion
        
        private string GivenValidGetTickerDetailsResponse()
        {
            var response = _fixture.Create<TickerDetails>();

            var json = JsonSerializer.Serialize(response);

            return json;
        }

        private string GivenValidPolygonGetTickersResponse()
        {
            var response = _fixture.Create<PolygonGetTickersResponse>();
            response.NextUrl = null;

            var json = JsonSerializer.Serialize(response);

            return json;
        }
    }
}