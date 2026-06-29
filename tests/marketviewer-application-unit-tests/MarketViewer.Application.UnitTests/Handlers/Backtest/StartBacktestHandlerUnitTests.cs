//using Amazon.DynamoDBv2;
//using Amazon.DynamoDBv2.DataModel;
//using Amazon.DynamoDBv2.Model;
//using Amazon.Lambda;
//using Amazon.Lambda.Model;
//using MarketViewer.Application.Handlers.Backtest;
//using MarketViewer.Contracts.Enums.Backtest;
//using MarketViewer.Contracts.Requests.Backtest;
//using MarketViewer.Core.Config;
//using Microsoft.Extensions.Logging;
//using Moq;
//using System.Net;
//using System.Threading;
//using System.Threading.Tasks;
//using Xunit;

//namespace MarketViewer.Application.UnitTests.Handlers.Backtest;

//public class StartBacktestHandlerTests
//{
//    private readonly Mock<IAmazonLambda> _mockLambdaClient;
//    private readonly Mock<IDynamoDBContext> _mockDynamoDbClient;
//    private readonly Mock<ILogger<StartBacktestHandler>> _mockLogger;
//    private readonly ServiceConfigs _configuration;
//    private readonly StartBacktestHandler _handler;

//    public StartBacktestHandlerTests()
//    {
//        _mockLambdaClient = new Mock<IAmazonLambda>();
//        _mockDynamoDbClient = new Mock<IDynamoDBContext>();
//        _mockLogger = new Mock<ILogger<StartBacktestHandler>>();
//        _configuration = new ServiceConfigs
//        {
//            BacktestStore = "backtest-table",
//            BacktestOrchestrator = "backtest-function"
//        };

//        _handler = new StartBacktestHandler(
//            _mockLambdaClient.Object,
//            _mockDynamoDbClient.Object,
//            _configuration,
//            _mockLogger.Object);
//    }

//    [Fact]
//    public async Task Handle_WithValidRequest_ReturnsSuccess()
//    {
//        // Arrange
//        var request = new StartBacktestRequest
//        {
//            Id = "test-id",
//            UserId = "user-id"
//        };

//        _mockDynamoDbClient
//            .Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
//            .ReturnsAsync(new PutItemResponse { HttpStatusCode = HttpStatusCode.OK });

//        _mockLambdaClient
//            .Setup(x => x.InvokeAsync(It.IsAny<InvokeRequest>(), It.IsAny<CancellationToken>()))
//            .ReturnsAsync(new InvokeResponse { HttpStatusCode = HttpStatusCode.OK });

//        // Act
//        var result = await _handler.Handle(request, CancellationToken.None);

//        // Assert
//        Assert.Equal(HttpStatusCode.OK, result.Status);
//        Assert.Equal("test-id", result.Data.Id);
//        Assert.Equal(BacktestStatus.Pending, result.Data.Status);
//    }

//    [Theory]
//    [InlineData(null)]
//    [InlineData("")]
//    public async Task Handle_WithInvalidRequest_ReturnsBadRequest(string id)
//    {
//        // Arrange
//        var request = new StartBacktestRequest { Id = id };

//        // Act
//        var result = await _handler.Handle(request, CancellationToken.None);

//        // Assert
//        Assert.Equal(HttpStatusCode.BadRequest, result.Status);
//        Assert.Contains("Invalid request.", result.ErrorMessages);
//    }

//    [Fact]
//    public async Task Handle_WhenDynamoDbFails_ReturnsInternalServerError()
//    {
//        // Arrange
//        var request = new StartBacktestRequest
//        {
//            Id = "test-id",
//            UserId = "user-id"
//        };

//        _mockDynamoDbClient
//            .Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
//            .ReturnsAsync(new PutItemResponse { HttpStatusCode = HttpStatusCode.InternalServerError });

//        // Act
//        var result = await _handler.Handle(request, CancellationToken.None);

//        // Assert
//        Assert.Equal(HttpStatusCode.InternalServerError, result.Status);
//        Assert.Contains("Internal server error.", result.ErrorMessages);
//        _mockLogger.Verify(
//            x => x.Log(
//                LogLevel.Error,
//                It.IsAny<EventId>(),
//                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Unable to store backtest record")),
//                It.IsAny<Exception>(),
//                It.IsAny<Func<It.IsAnyType, Exception, string>>()
//            ),
//            Times.Once);
//    }

//    [Fact]
//    public async Task Handle_WhenLambdaFails_ReturnsInternalServerError()
//    {
//        // Arrange
//        var request = new StartBacktestRequest
//        {
//            Id = "test-id",
//            UserId = "user-id"
//        };

//        _mockDynamoDbClient
//            .Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
//            .ReturnsAsync(new PutItemResponse { HttpStatusCode = HttpStatusCode.OK });

//        _mockLambdaClient
//            .Setup(x => x.InvokeAsync(It.IsAny<InvokeRequest>(), It.IsAny<CancellationToken>()))
//            .ReturnsAsync(new InvokeResponse { HttpStatusCode = HttpStatusCode.InternalServerError });

//        // Act
//        var result = await _handler.Handle(request, CancellationToken.None);

//        // Assert
//        Assert.Equal(HttpStatusCode.InternalServerError, result.Status);
//        Assert.Contains("Internal server error.", result.ErrorMessages);
//        _mockLogger.Verify(
//            x => x.Log(
//                LogLevel.Error,
//                It.IsAny<EventId>(),
//                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Unable to start backtest")),
//                It.IsAny<Exception>(),
//                It.IsAny<Func<It.IsAnyType, Exception, string>>()
//            ),
//            Times.Once);
//    }
//}