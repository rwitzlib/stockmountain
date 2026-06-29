//using AutoFixture;
//using Moq;
//using Xunit;
//using FluentAssertions;
//using Moq.AutoMock;
//using MarketViewer.Infrastructure.Services;
//using AutoMapper;
//using MarketViewer.Contracts.Requests;
//using MarketViewer.Infrastructure.Mapping;
//using MarketDataProvider.Contracts.Responses.PolygonApi;
//using MarketDataProvider.Contracts.Requests.PolygonApi;

//namespace MarketViewer.Infrastructure.UnitTests.Services
//{
//    public class MarketDataServiceUnitTests
//    {
//        #region Private Fields
//        private Fixture _autoFixture;
//        private AutoMocker _autoMocker;

//        private Mock<IMapper> _mapper;
//        //private Mock<IPolygonClient> _polygonClient;

//        private MarketDataRepository _classUnderTest;
//        #endregion

//        #region Constructor
//        public MarketDataServiceUnitTests()
//        {
//            _autoFixture = new Fixture();
//            _autoMocker = new AutoMocker();

//            var profile = new AggregateProfile();
//            var configuration = new MapperConfiguration(cfg => cfg.AddProfiles(new[] { profile }));
//            var mapper = new Mapper(configuration);

//            _polygonClient = new Mock<IPolygonClient>();

//            _classUnderTest = new MarketDataRepository(_polygonClient.Object, mapper);
//        }
//        #endregion

//        [Fact]
//        public async Task GetAggregateAsync_Returns_SuccessResponse()
//        {
//            // Arrange
//            var request = _autoFixture.Create<StocksRequest>();
//            var response = _autoFixture.Create<PolygonAggregateResponse>();
//            response.Ticker = request.Ticker;

//            _polygonClient.Setup(q => q.GetAggregate(It.IsAny<PolygonAggregateRequest>()))
//                .ReturnsAsync(response);

//            // Act
//            var result = await _classUnderTest.GetStockDataAsync(request);

//            // Assert
//            result.Ticker.Should().BeEquivalentTo(request.Ticker);

//            _polygonClient.Verify(q => q.GetAggregate(It.IsAny<PolygonAggregateRequest>()), Times.Once());
//            _polygonClient.Verify(q => q.GetTickerDetails(It.IsAny<string>()), Times.Once());
//        }

//        [Fact]
//        public async Task GetAggregate_And_GetTickerDetails_Returns_SuccessResponse()
//        {
//            // Arrange
//            var request = _autoFixture.Create<StocksRequest>();
//            var response = _autoFixture.Create<PolygonAggregateResponse>();
//            response.Ticker = request.Ticker;

//            _polygonClient.Setup(q => q.GetAggregate(It.IsAny<PolygonAggregateRequest>()))
//                .ReturnsAsync(response);

//            _polygonClient.Setup(q => q.GetTickerDetails(It.IsAny<string>()))
//                .ReturnsAsync(_autoFixture.Create<PolygonTickerDetailsResponse>());

//            // Act
//            var result = await _classUnderTest.GetStockDataAsync(request);

//            // Assert
//            result.Ticker.Should().BeEquivalentTo(request.Ticker);
//            result.TickerDetails.Should().NotBeNull();

//            _polygonClient.Verify(q => q.GetAggregate(It.IsAny<PolygonAggregateRequest>()), Times.Once());
//            _polygonClient.Verify(q => q.GetTickerDetails(It.IsAny<string>()), Times.Once());
//        }

//        [Fact]
//        public async Task Null_GetAggregate_Response_Returns_Null()
//        {
//            // Arrange
//            var request = _autoFixture.Create<StocksRequest>();
//            _polygonClient.Setup(q => q.GetAggregate(It.IsAny<PolygonAggregateRequest>()))
//                .ReturnsAsync((PolygonAggregateResponse)null);

//            _polygonClient.Setup(q => q.GetTickerDetails(It.IsAny<string>()))
//                .ReturnsAsync(_autoFixture.Create<PolygonTickerDetailsResponse>());

//            // Act
//            var result = await _classUnderTest.GetStockDataAsync(request);

//            // Assert
//            result.Should().BeNull();

//            _polygonClient.Verify(q => q.GetAggregate(It.IsAny<PolygonAggregateRequest>()), Times.Once());
//            _polygonClient.Verify(q => q.GetTickerDetails(It.IsAny<string>()), Times.Never());
//        }

//        [Fact]
//        public async Task Null_GetTickerDetails_Response_Returns_Null_TickerDetails()
//        {
//            // Arrange
//            var request = _autoFixture.Create<StocksRequest>();
//            var response = _autoFixture.Create<PolygonAggregateResponse>();
//            response.Ticker = request.Ticker;

//            _polygonClient.Setup(q => q.GetAggregate(It.IsAny<PolygonAggregateRequest>()))
//                .ReturnsAsync(response);

//            _polygonClient.Setup(q => q.GetTickerDetails(It.IsAny<string>()))
//                .ReturnsAsync((PolygonTickerDetailsResponse)null);

//            // Act
//            var result = await _classUnderTest.GetStockDataAsync(request);

//            // Assert
//            result.Ticker.Should().BeEquivalentTo(request.Ticker);
//            result.TickerDetails.Should().BeNull();

//            _polygonClient.Verify(q => q.GetAggregate(It.IsAny<PolygonAggregateRequest>()), Times.Once());
//            _polygonClient.Verify(q => q.GetTickerDetails(It.IsAny<string>()), Times.Once());
//        }

//        [Fact]
//        public async Task Client_Throws_Exception_Returns_Null()
//        {
//            // Arrange
//            var request = _autoFixture.Create<StocksRequest>();

//            _polygonClient.Setup(q => q.GetAggregate(It.IsAny<PolygonAggregateRequest>()))
//                .ThrowsAsync(new Exception("asdf"));

//            // Act
//            var result = await _classUnderTest.GetStockDataAsync(request);

//            // Assert
//            result.Should().BeNull();

//            _polygonClient.Verify(q => q.GetAggregate(It.IsAny<PolygonAggregateRequest>()), Times.Once());
//            _polygonClient.Verify(q => q.GetTickerDetails(It.IsAny<string>()), Times.Never());
//        }

//        [Fact]
//        public async Task GetTickerDetailsAsync_Returns_Success_Response()
//        {
//            // Arrange
//            var response = _autoFixture.Create<PolygonTickerDetailsResponse>();

//            _polygonClient.Setup(q => q.GetTickerDetails(It.IsAny<string>()))
//                .ReturnsAsync(response);

//            // Act
//            var result = await _classUnderTest.GetTickerDetailsAsync(response.TickerDetails.Ticker);

//            // Assert
//            result.Ticker.Should().BeEquivalentTo(response.TickerDetails.Ticker);

//            _polygonClient.Verify(q => q.GetTickerDetails(It.IsAny<string>()), Times.Once());
//        }

//        [Fact]
//        public async Task GetTickerDetailsAsync_Null_Ticker_Returns_Null_Response()
//        {
//            // Act
//            var result = await _classUnderTest.GetTickerDetailsAsync(null);

//            // Assert
//            result.Should().BeNull();

//            _polygonClient.Verify(q => q.GetTickerDetails(It.IsAny<string>()), Times.Never());
//        }

//        [Fact]
//        public async Task Client_Null_Response_Returns_Null_Response()
//        {
//            // Act
//            var result = await _classUnderTest.GetTickerDetailsAsync("TSLA");

//            // Assert
//            result.Should().BeNull();

//            _polygonClient.Verify(q => q.GetTickerDetails(It.IsAny<string>()), Times.Once());
//        }

//        [Fact]
//        public async Task Client_Null_TickerDetails_Response_Returns_Null_Response()
//        {
//            // Arrange
//            var response = _autoFixture.Create<PolygonTickerDetailsResponse>();
//            response.TickerDetails = null;

//            _polygonClient.Setup(q => q.GetTickerDetails(It.IsAny<string>()))
//                .ReturnsAsync(response);

//            // Act
//            var result = await _classUnderTest.GetTickerDetailsAsync("TSLA");

//            // Assert
//            result.Should().BeNull();

//            _polygonClient.Verify(q => q.GetTickerDetails(It.IsAny<string>()), Times.Once());
//        }
//    }
//}
