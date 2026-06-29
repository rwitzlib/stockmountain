//using AutoFixture;
//using MarketViewer.Api.Controllers;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Extensions.Logging;
//using Moq;
//using System.Net;
//using Xunit;
//using FluentAssertions;
//using MarketViewer.Contracts.Interfaces;
//using MarketDataProvider.Contracts.Models;

//namespace MarketViewer.Api.UnitTests.Controllers
//{
//    public class TickersControllerUnitTests
//    {
//        private TickerDetailsController _classUnderTest;
//        private Fixture _autoFixture;
//        private Mock<IMarketDataRepository> _marketDataService;
//        private Mock<ILogger<TickerDetailsController>> _logger;

//        public TickersControllerUnitTests()
//        {
//            _autoFixture = new Fixture();
//            _marketDataService = new Mock<IMarketDataRepository>();
//            _logger = new Mock<ILogger<TickerDetailsController>>();

//            _classUnderTest = new TickerDetailsController(_logger.Object, _marketDataService.Object);
//        }

//        [Fact]
//        public async Task HandleScanRequest_Returns_Valid_Response()
//        {
//            // Arrange
//            var ticker = _autoFixture.Create<string>();

//            _marketDataService.Setup(methods => methods.GetTickerDetailsAsync(It.IsAny<string>()))
//                .ReturnsAsync(_autoFixture.Create<TickerDetails>());

//            // Act
//            var response = await _classUnderTest.HandleTickerDetailsRequest(ticker);

//            // Assert
//            var result = Assert.IsType<OkObjectResult>(response);
//            result.Value.Should().BeOfType<TickerDetails>();
//        }

//        [Fact]
//        public async Task HandleScanRequest_Returns_Null_Response()
//        {
//            // Arrange
//            var ticker = _autoFixture.Create<string>();

//            _marketDataService.Setup(methods => methods.GetTickerDetailsAsync(It.IsAny<string>()))
//                .ReturnsAsync((TickerDetails)null);

//            // Act
//            var response = await _classUnderTest.HandleTickerDetailsRequest(ticker);

//            // Assert
//            var result = Assert.IsType<ObjectResult>(response);
//            result.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
//            result.Value.Should().BeOfType<List<string>>().Which.Should().NotBeNullOrEmpty();
//        }

//        [Fact]
//        public async Task HandleScanRequest_With_Null_Ticker_Returns_InternalServerError_Response()
//        {
//            // Act
//            var response = await _classUnderTest.HandleTickerDetailsRequest(null);

//            // Assert
//            var result = Assert.IsType<BadRequestObjectResult>(response);
//            result.StatusCode.Value.Should().Be((int)HttpStatusCode.BadRequest);
//            result.Value.Should().BeOfType<List<string>>().Which.Should().NotBeNullOrEmpty();
//        }

//        [Fact]
//        public async Task HandleScanRequest_Throws_Exception_Returns_InternalServerError()
//        {
//            // Arrange
//            var ticker = _autoFixture.Create<string>();

//            _marketDataService.Setup(methods => methods.GetTickerDetailsAsync(It.IsAny<string>()))
//                .ThrowsAsync(new Exception());

//            // Act
//            var response = await _classUnderTest.HandleTickerDetailsRequest(ticker);

//            // Assert
//            var result = Assert.IsType<ObjectResult>(response);
//            result.StatusCode.Value.Should().Be((int)HttpStatusCode.InternalServerError);
//            result.Value.Should().BeOfType<List<string>>().Which.Should().NotBeNullOrEmpty();
//        }
//    }
//}
