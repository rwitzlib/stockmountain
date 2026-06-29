using AutoFixture;
using AutoMapper;
using Moq;
using Xunit;
using FluentAssertions;
using MarketViewer.Contracts.Interfaces;
using MarketViewer.Contracts.Enums;
using Polygon.Client.Models;
using MarketViewer.Studies.UnitTests;
using MarketViewer.Application.Handlers.Market;
using MarketViewer.Contracts.Requests.Market;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Models.Indicator;
using Microsoft.Extensions.Logging.Abstractions;
using MarketViewer.Application.Services;

namespace MarketViewer.Application.UnitTests.Handlers
{
    public class StocksHandlerUnitTests : IClassFixture<StudyFixture>
    {
        #region Private Fields
        private readonly StocksHandler _classUnderTest;
        private readonly Fixture _fixture;
        private readonly Mock<IMarketDataRepository> _repository;
        private readonly Mock<IMarketCache> _cache;
        #endregion

        #region Constructor
        public StocksHandlerUnitTests(StudyFixture fixture)
        {
            _fixture = new Fixture();

            _repository = new Mock<IMarketDataRepository>();
            _cache = new Mock<IMarketCache>();
            var indicatorCalculator = new IndicatorCalculationService(fixture.StudyFactory, NullLogger<IndicatorCalculationService>.Instance);
            _classUnderTest = new StocksHandler(_repository.Object, _cache.Object, indicatorCalculator, new NullLogger<StocksHandler>());
        }
        #endregion

        #region Tests
        [Fact]
        public async Task Valid_GetAggregate_Request_Returns_SuccessResponse()
        {
            // Arrange
            var request = GivenAggregateRequestWithNoStudies();
            var response = GivenSuccessfulResponse();

            _repository.Setup(q => q.GetStockDataAsync(It.IsAny<StocksRequest>()))
                .ReturnsAsync(response);

            // Act
            var result = await _classUnderTest.Handle(request, default);

            // Assert
            result.Data.Results.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task GetAggregate_With_Null_Response_From_Repository_Is_Failure()
        {
            // Arrange
            var request = GivenAggregateRequestWithNoStudies();
            var response = GivenSuccessfulResponse();

            // Act
            var result = await _classUnderTest.Handle(request, default);

            // Assert
            result.ErrorMessages.Should().Contain("Query returned invalid result.");
        }

        [Fact]
        public async Task GetAggregate_Response_With_Non_OK_Status_Is_Success()
        {
            // Arrange
            var request = GivenAggregateRequestWithNoStudies();
            var response = GivenSuccessfulResponse();

            _repository.Setup(q => q.GetStockDataAsync(It.IsAny<StocksRequest>()))
                .ReturnsAsync(response);

            // Act
            var result = await _classUnderTest.Handle(request, default);

            // Assert
            result.Data.Results.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task GetAggregate_Response_With_No_Results_Is_Failure()
        {
            // Arrange
            var request = GivenAggregateRequestWithNoStudies();
            var response = new StocksResponse
            {
                Ticker = _fixture.Create<string>(),
                Status = _fixture.Create<string>(),
            };

            _repository.Setup(q => q.GetStockDataAsync(It.IsAny<StocksRequest>()))
                .ReturnsAsync(response);

            // Act
            var result = await _classUnderTest.Handle(request, default);

            // Assert
            result.ErrorMessages.Should().Contain("Query returned no results.");
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public async Task GetAggregate_With_Invalid_Ticker_Request_Is_Failure(string ticker)
        {
            // Arrange
            var request = GivenAggregateRequestWithNoStudies();
            request.Ticker = ticker;

            // Act
            var result = await _classUnderTest.Handle(request, default);

            // Assert
            result.ErrorMessages.Should().Contain("Must include a valid Ticker.");
        }

        [Fact]
        public async Task GetAggregate_With_Too_Early_From_Date_Is_Failure()
        {
            // Arrange
            var request = GivenAggregateRequestWithNoStudies();
            request.From = DateTimeOffset.UnixEpoch.AddDays(-1);

            // Act
            var result = await _classUnderTest.Handle(request, default);

            // Assert
            result.ErrorMessages.Should().Contain($"'From' date must be later than {DateTimeOffset.UnixEpoch:yyyy-MM-dd}.");
        }

        [Fact]
        public async Task GetAggregate_With_Invalid_From_Date_Request_Is_Failure()
        {
            // Arrange
            var request = GivenAggregateRequestWithNoStudies();
            request.From = DateTimeOffset.Now.AddDays(1);

            // Act
            var result = await _classUnderTest.Handle(request, default);

            // Assert
            result.ErrorMessages.Should().Contain($"'From' date must be earlier than {DateTimeOffset.Now:yyyy-MM-dd}.");
        }

        [Fact]
        public async Task GetAggregate_With_Invalid_From_And_To_Date_Request_Is_Failure()
        {
            // Arrange
            var request = GivenAggregateRequestWithNoStudies();
            request.From = DateTimeOffset.Now.AddDays(-1);
            request.To = DateTimeOffset.Now.AddDays(-2);

            // Act
            var result = await _classUnderTest.Handle(request, default);

            // Assert
            result.ErrorMessages.Should().Contain("'From' date must be earlier than 'To' date.");
        }

        [Fact]
        public async Task GetAggregateAsync_With_Invalid_Ticker_And_From_Date_Request_Is_Failure()
        {
            // Arrange
            var request = GivenAggregateRequestWithStudies();
            request.Ticker = string.Empty;
            request.From = DateTimeOffset.Now.AddDays(1);

            // Act
            var result = await _classUnderTest.Handle(request, default);

            // Assert
            result.ErrorMessages.Should().Contain("Must include a valid Ticker.");
            result.ErrorMessages.Should().Contain($"'From' date must be earlier than {DateTimeOffset.Now:yyyy-MM-dd}.");
        }

        [Fact]
        public async Task GetAggregate_With_Valid_Study_Should_Return_Response_With_Study()
        {
            // Arrange
            var request = GivenAggregateRequestWithStudies();
            var response = GivenSuccessfulResponse();

            _repository.Setup(q => q.GetStockDataAsync(It.IsAny<StocksRequest>()))
                .ReturnsAsync(response);

            // Act
            var result = await _classUnderTest.Handle(request, default);

            // Assert
            result.Data.Results.Should().NotBeNullOrEmpty();
            result.Data.Indicators.First().Name.Should().NotBeNullOrEmpty();
            result.Data.Indicators.First().Results.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task GetAggregate_With_Invalid_Study_Parameters_Should_Return_Response_With_No_Studies()
        {
            // Arrange
            var request = GivenAggregateRequestWithStudies();
            request.Indicators.First().Parameters = new[] { "12", "26", "9" };
            var response = GivenSuccessfulResponse();

            _repository.Setup(q => q.GetStockDataAsync(It.IsAny<StocksRequest>()))
                .ReturnsAsync(response);

            // Act
            var result = await _classUnderTest.Handle(request, default);

            // Assert
            result.Data.Results.Should().NotBeNullOrEmpty();
            result.Data.Indicators.Should().BeNullOrEmpty();
        }

        [Fact]
        public async Task RequestWithLimitShouldOnlyReturnThatManyResults_WithStudies()
        {
            // Arrange
            var request = GivenAggregateRequestWithStudies();
            request.Limit = 10;

            var response = GivenSuccessfulResponse();

            _repository.Setup(q => q.GetStockDataAsync(It.IsAny<StocksRequest>()))
                .ReturnsAsync(response);

            // Act
            var result = await _classUnderTest.Handle(request, default);

            // Assert
            result.Data.Results.Should().NotBeNullOrEmpty();
            result.Data.Results.Count().Should().Be(10);

            foreach (var study in result.Data.Indicators)
            {
                study.Results.Count.Should().Be(10);
            }
        }

        [Fact]
        public async Task RequestWithLimitShouldOnlyReturnThatManyResults_NoStudies()
        {
            // Arrange
            var request = GivenAggregateRequestWithNoStudies();
            request.Limit = 10;

            var response = GivenSuccessfulResponse();

            _repository.Setup(q => q.GetStockDataAsync(It.IsAny<StocksRequest>()))
                .ReturnsAsync(response);

            // Act
            var result = await _classUnderTest.Handle(request, default);

            // Assert
            result.Data.Results.Should().NotBeNullOrEmpty();
            result.Data.Results.Count().Should().Be(10);
            result.Data.Indicators.Should().BeNullOrEmpty();
        }

        #endregion

        #region Private Methods
        private StocksRequest GivenAggregateRequestWithNoStudies()
        {
            var request = new StocksRequest
            {
                Ticker = "AAPL",
                Multiplier = 1,
                Timespan = Timespan.minute,
                From = DateTimeOffset.Now.AddDays(-1),
                To = DateTimeOffset.Now
            };

            return request;
        }

        private static StocksRequest GivenAggregateRequestWithStudies()
        {
            var request = new StocksRequest
            {
                Ticker = "AAPL",
                Multiplier = 1,
                Timespan = Timespan.minute,
                From = DateTimeOffset.Now.AddDays(-1),
                To = DateTimeOffset.Now,
                Indicators =
                [
                    new Indicator
                    {
                        Type = StudyType.macd,
                        Parameters = ["12", "26", "9", "EMA"]
                    }
                ]
            };

            return request;
        }

        private StocksResponse GivenSuccessfulResponse()
        {
            var response = new StocksResponse
            {
                Ticker = "AAPL",
                Status = "OK",
                Results = _fixture.CreateMany<Bar>(100).ToList()
            };

            return response;
        }
        #endregion
    }
}
