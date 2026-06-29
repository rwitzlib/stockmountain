//using AutoFixture;
//using Moq;
//using Xunit;
//using FluentAssertions;
//using Moq.AutoMock;
//using AutoMapper;
//using MarketViewer.Contracts.Requests;
//using MarketViewer.Infrastructure.Mapping;
//using Microsoft.Extensions.Caching.Memory;
//using Amazon.S3;
//using System.Text.Json;
//using Amazon.S3.Model;
//using MarketDataProvider.Contracts.Responses.PolygonApi;
//using MarketDataProvider.Contracts.Models;
//using MarketViewer.Infrastructure.Services;

//namespace MarketViewer.Infrastructure.UnitTests.Services
//{
//    public class BacktestMarketDataCacheUnitTests
//    {
//        #region Private Fields
//        private Fixture _autoFixture;
//        private AutoMocker _autoMocker;

//        private Mock<IMemoryCache> _memoryCache;
//        private Mock<IAmazonS3> _amazonClient;

//        private BacktestMarketDataCache _classUnderTest;
//        #endregion

//        #region Constructor
//        public BacktestMarketDataCacheUnitTests()
//        {
//            _autoFixture = new Fixture();
//            _autoMocker = new AutoMocker();

//            var profile = new AggregateProfile();
//            var configuration = new MapperConfiguration(cfg => cfg.AddProfiles(new[] { profile }));
//            var mapper = new Mapper(configuration);

//            _memoryCache = new Mock<IMemoryCache>();
//            _amazonClient = new Mock<IAmazonS3>();

//            _classUnderTest = new BacktestMarketDataCache(_amazonClient.Object, _memoryCache.Object, mapper);
//        }
//        #endregion

//        [Fact]
//        public async Task AWS_S3_Returns_Null_Response_Returns_Empty_Response()
//        {
//            // Arrange
//            var request = _autoFixture.Create<ScanRequest>();

//            // Act
//            var result = await _classUnderTest.RetrieveAggregateResponses(request);

//            // Assert
//            result.Should().NotBeNull();
//            result.Should().BeEmpty();
//        }

//        [Fact]
//        public async Task AWS_S3_Returns_Response_With_Too_Few_Candles_Returns_Empty_Response()
//        {
//            // Arrange
//            var request = _autoFixture.Create<ScanRequest>();
//            GivenS3ReturnsAggregates();

//            // Act
//            var result = await _classUnderTest.RetrieveAggregateResponses(request);

//            // Assert
//            //result.Should().NotBeNullOrEmpty();
//        }

//        [Fact]
//        public async Task Cache_Returns_Response_With_Too_Few_Candles()
//        {
//            // Arrange
//            var request = _autoFixture.Create<ScanRequest>();
//            var aggregates = _autoFixture.CreateMany<PolygonAggregateResponse>(100);
//            //_memoryCache.Setup(q => q.TryGetValue(It.IsAny<string>(), out aggregates));

//            // Act
//            var result = await _classUnderTest.RetrieveAggregateResponses(request);

//            // Assert
//            //result.Should().NotBeNull();
//            //result.Should().BeEmpty();
//        }

//        [Fact]
//        public async Task Cache_Returns_Valid_Response()
//        {
//            // Arrange
//            var request = _autoFixture.Create<ScanRequest>();
//            var aggregates = _autoFixture.CreateMany<PolygonAggregateResponse>(100);
//            aggregates.First().Results = _autoFixture.CreateMany<Candle>(100).ToList();
//            //_memoryCache.Setup(q => q.TryGetValue(It.IsAny<string>(), out aggregates));

//            // Act
//            var result = await _classUnderTest.RetrieveAggregateResponses(request);

//            // Assert
//            //result.Should().NotBeNullOrEmpty();
//        }

//        private void GivenS3ReturnsAggregates()
//        {
//            var aggregates = _autoFixture.CreateMany<PolygonAggregateResponse>(100);
//            var json = JsonSerializer.Serialize(aggregates);

//            var stream = new MemoryStream();

//            var writer = new StreamWriter(stream);
//            writer.Write(json);

//            _amazonClient.Setup(q => q.GetObjectAsync(It.IsAny<GetObjectRequest>(), default))
//                .ReturnsAsync(new GetObjectResponse
//                {
//                    ResponseStream = stream
//                });
//        }
//    }
//}
