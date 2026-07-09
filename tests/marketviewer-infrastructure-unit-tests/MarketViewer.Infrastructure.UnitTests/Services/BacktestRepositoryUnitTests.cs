using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using FluentAssertions;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Enums.Backtest;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Models.Strategy;
using MarketViewer.Contracts.Records.Backtest;
using MarketViewer.Contracts.Requests.Market.Backtest;
using MarketViewer.Infrastructure.Config;
using MarketViewer.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Document = Amazon.DynamoDBv2.DocumentModel.Document;

namespace MarketViewer.Infrastructure.UnitTests.Services;

public class BacktestRepositoryUnitTests
{
    private readonly Mock<IAmazonDynamoDB> _dynamoDb = new();
    private readonly BacktestRepository _repository;

    public BacktestRepositoryUnitTests()
    {
        var config = new BacktestConfig
        {
            TableName = "backtest-store",
            UserIndexName = "UserIndex",
            S3BucketName = "bucket"
        };

        _repository = new BacktestRepository(config, _dynamoDb.Object, new Mock<IAmazonS3>().Object, NullLogger<BacktestRepository>.Instance);
    }

    [Fact]
    public async Task Get_Returns_Record_Written_By_Put()
    {
        Dictionary<string, AttributeValue> storedItem = null;
        _dynamoDb.Setup(q => q.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutItemRequest, CancellationToken>((request, _) => storedItem = request.Item)
            .ReturnsAsync(new PutItemResponse());

        var record = CreateRecord();

        var putResult = await _repository.Put(record);

        putResult.Should().BeTrue();
        storedItem.Should().NotBeNull();
        storedItem["PK"].S.Should().Be(record.Id);
        storedItem["SK"].S.Should().Be("Context");
        storedItem["Id"].S.Should().Be(record.Id);

        _dynamoDb.Setup(q => q.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse { Item = storedItem });

        var result = await _repository.Get(record.Id);

        result.Should().NotBeNull();
        result.Id.Should().Be(record.Id);
        result.UserId.Should().Be(record.UserId);
        result.Status.Should().Be(record.Status);
        result.Request.Should().NotBeNull();
        result.Request.Start.Should().Be(record.Request.Start);
        result.Request.End.Should().Be(record.Request.End);
        result.Request.EntrySettings.Filters.Should().BeEquivalentTo(record.Request.EntrySettings.Filters);
        result.Request.PositionSettings.StartingBalance.Should().Be(record.Request.PositionSettings.StartingBalance);
    }

    [Fact]
    public async Task Get_Maps_Legacy_Record_With_Request_Stored_As_Map()
    {
        // Records written by the lambda before consolidation stored the whole record,
        // including Request, as native DynamoDB maps instead of a serialized JSON string.
        var record = CreateRecord();
        var item = Document.FromJson(JsonSerializer.Serialize(record)).ToAttributeMap();
        item["PK"] = new AttributeValue { S = record.Id };
        item["SK"] = new AttributeValue { S = "Context" };

        _dynamoDb.Setup(q => q.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse { Item = item });

        var result = await _repository.Get(record.Id);

        result.Should().NotBeNull();
        result.Id.Should().Be(record.Id);
        result.Request.Should().NotBeNull();
        result.Request.EntrySettings.Filters.Should().BeEquivalentTo(record.Request.EntrySettings.Filters);
    }

    [Fact]
    public async Task Get_Returns_Null_When_Item_Missing()
    {
        _dynamoDb.Setup(q => q.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse { Item = null });

        var result = await _repository.Get("does-not-exist");

        result.Should().BeNull();
    }

    private static BacktestContextRecord CreateRecord()
    {
        return new BacktestContextRecord
        {
            Id = "backtest-1",
            UserId = "user-1",
            Status = BacktestStatus.Pending,
            CreatedAt = "2026-07-08T09:30:00Z-04:00",
            Start = "2026-06-01",
            End = "2026-06-30",
            CreditsUsed = 12.5f,
            HoldProfit = 100f,
            HighProfit = 150f,
            ConditionalProfit = 0f,
            DurationSeconds = 42f,
            Request = new BacktestCreateRequest
            {
                Start = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
                End = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero),
                PositionSettings = new StrategyPositionSettings
                {
                    StartingBalance = 10000,
                    MaxConcurrentPositions = 5,
                    Model = new PositionModel { Type = PositionType.Fixed, Size = 1000 },
                    Cooldown = new Timeframe(5, Timespan.minute)
                },
                EntrySettings = new StrategyEntrySettings
                {
                    Filters = ["filter-1", "filter-2"]
                },
                ExitSettings = new StrategyExitSettings
                {
                    StopLoss = new Exit { Type = default, Value = 5 },
                    TakeProfit = new Exit { Type = default, Value = 10 },
                    TimedExit = new TimedExit { Timeframe = new Timeframe(1, Timespan.day) }
                }
            }
        };
    }
}
