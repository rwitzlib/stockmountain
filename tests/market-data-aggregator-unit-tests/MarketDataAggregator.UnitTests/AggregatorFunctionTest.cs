using Amazon.DynamoDBv2;
using Amazon.Lambda.TestUtilities;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using FluentValidation;
using MarketDataAggregator;
using MarketDataAggregator.Validation;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.MarketData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using Massive.Client.Interfaces;
using Massive.Client.Requests;
using Xunit;

namespace MarketDataAggregator.UnitTests;

public class AggregatorFunctionTest
{
    private readonly AutoMocker _autoMocker = new();

    [Theory]
    [InlineData("asdf", "2023-09-25")]
    [InlineData(null, "2023-09-25", 31)]
    [InlineData(null, "2023-09-24", 1)]
    public async Task Invalid_Input_Should_Return_Immediately(string type, string date, int multiplier = 1)
    {
        var request = new MarketDataAggregatorRequest
        {
            Date = DateTimeOffset.Parse(date),
            Type = type,
            Multiplier = multiplier,
            Timespan = Timespan.minute
        };

        var services = new ServiceCollection();
        services.AddSingleton(_autoMocker.GetMock<IAmazonS3>().Object);
        services.AddSingleton(_autoMocker.GetMock<IAmazonDynamoDB>().Object);
        services.AddSingleton(_autoMocker.GetMock<IMassiveClient>().Object);
        services.AddSingleton<ILogger<AggregatorFunction>>(_autoMocker.GetMock<ILogger<AggregatorFunction>>().Object);
        services.AddSingleton<IValidator<MarketDataAggregatorRequest>, MarketDataAggregatorRequestValidator>();
        var serviceProvider = services.BuildServiceProvider();

        var function = new AggregatorFunction(serviceProvider);
        var context = new TestLambdaContext();

        await function.FunctionHandler(request, context);

        _autoMocker.GetMock<IAmazonS3>().Verify(q => q.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Never());
        _autoMocker.GetMock<IMassiveClient>().Verify(q => q.GetTickers(It.IsAny<MassiveGetTickersRequest>()), Times.Never());
        _autoMocker.GetMock<IMassiveClient>().Verify(q => q.GetAggregates(It.IsAny<MassiveAggregateRequest>()), Times.Never());
    }

    [Theory]
    [InlineData("2024-06-07", 1, Timespan.minute, "backtest/2024/06/07/aggregate_1_minute")]
    [InlineData("2024-06-07", 1, Timespan.hour, "backtest/2024/06/aggregate_1_hour")]
    [InlineData("2024-06-07", 1, Timespan.day, "backtest/2024/aggregate_1_day")]
    public void Shared_Storage_Contract_Should_Build_Expected_Aggregate_Keys(string date, int multiplier, Timespan timespan, string expectedKey)
    {
        var key = MarketDataStorageContract.BuildAggregateKey(DateTimeOffset.Parse(date), multiplier, timespan);

        key.Should().Be(expectedKey);
    }
}
