using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using MarketViewer.Contracts.Enums;
using MarketViewer.Infrastructure.Config;
using MarketViewer.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Globalization;
using System.Net;
using Xunit;

namespace MarketViewer.Infrastructure.UnitTests.Services;

public class UserRepositoryUnitTests
{
    private readonly Mock<IAmazonDynamoDB> _dynamo = new();
    private readonly UserRepository _repository;

    public UserRepositoryUnitTests()
    {
        _repository = new UserRepository(
            new UserConfig { TableName = "user-store" },
            _dynamo.Object,
            NullLogger<UserRepository>.Instance);
    }

    private void SetupUser(float credits, float purchasedCredits, string role = "Free", bool includePurchasedAttribute = true)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            { "Id", new AttributeValue { S = "user-1" } },
            { "Role", new AttributeValue { S = role } },
            { "AvatarUrl", new AttributeValue { S = "" } },
            { "IsPublic", new AttributeValue { S = "False" } },
            { "Credits", new AttributeValue { N = credits.ToString(CultureInfo.InvariantCulture) } },
            { "MaxCredits", new AttributeValue { N = "100" } }
        };

        if (includePurchasedAttribute)
        {
            item.Add("PurchasedCredits", new AttributeValue { N = purchasedCredits.ToString(CultureInfo.InvariantCulture) } );
        }

        _dynamo.Setup(d => d.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = item, HttpStatusCode = HttpStatusCode.OK });
    }

    [Fact]
    public async Task Get_LegacyRoleString_MapsToRenamedRole()
    {
        SetupUser(credits: 50, purchasedCredits: 0, role: "Basic", includePurchasedAttribute: false);

        var user = await _repository.Get("user-1");

        user.Role.Should().Be(UserRole.Free);
        user.PurchasedCredits.Should().Be(0);
    }

    [Fact]
    public async Task TryDebitCredits_MonthlyCoversCost_UsesAtomicDecrementOnly()
    {
        SetupUser(credits: 80, purchasedCredits: 40);
        UpdateItemRequest captured = null;
        _dynamo.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .Callback<UpdateItemRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new UpdateItemResponse { HttpStatusCode = HttpStatusCode.OK });

        var result = await _repository.TryDebitCredits("user-1", 30);

        result.Should().BeTrue();
        captured.UpdateExpression.Should().Be("SET Credits = Credits - :credits");
        captured.ConditionExpression.Should().Contain("Credits >= :credits");
    }

    [Fact]
    public async Task TryDebitCredits_CostSpansBothBalances_ZeroesMonthlyAndDebitsPurchased()
    {
        SetupUser(credits: 10, purchasedCredits: 50);
        UpdateItemRequest captured = null;
        _dynamo.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .Callback<UpdateItemRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new UpdateItemResponse { HttpStatusCode = HttpStatusCode.OK });

        var result = await _repository.TryDebitCredits("user-1", 30);

        result.Should().BeTrue();
        captured.UpdateExpression.Should().Be("SET Credits = :zero, PurchasedCredits = :newPurchased");
        captured.ExpressionAttributeValues[":newPurchased"].N.Should().Be("30"); // 50 - (30 - 10)
        captured.ExpressionAttributeValues[":expectedCredits"].N.Should().Be("10");
        captured.ExpressionAttributeValues[":expectedPurchased"].N.Should().Be("50");
    }

    [Fact]
    public async Task TryDebitCredits_InsufficientCombinedBalance_FailsWithoutWriting()
    {
        SetupUser(credits: 10, purchasedCredits: 5);

        var result = await _repository.TryDebitCredits("user-1", 30);

        result.Should().BeFalse();
        _dynamo.Verify(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default), Times.Never);
    }

    [Fact]
    public async Task TryDebitCredits_LegacyRecordWithoutPurchasedAttribute_DebitsMonthly()
    {
        SetupUser(credits: 80, purchasedCredits: 0, includePurchasedAttribute: false);
        _dynamo.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse { HttpStatusCode = HttpStatusCode.OK });

        var result = await _repository.TryDebitCredits("user-1", 30);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryDebitCredits_ConcurrentBalanceChange_RetriesOnceThenSucceeds()
    {
        SetupUser(credits: 10, purchasedCredits: 50);
        var calls = 0;
        _dynamo.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(() =>
            {
                calls++;
                if (calls == 1)
                {
                    throw new ConditionalCheckFailedException("balance moved");
                }
                return new UpdateItemResponse { HttpStatusCode = HttpStatusCode.OK };
            });

        var result = await _repository.TryDebitCredits("user-1", 30);

        result.Should().BeTrue();
        calls.Should().Be(2);
    }

    [Fact]
    public async Task TryDebitCredits_BalanceKeepsChanging_GivesUpAfterRetry()
    {
        SetupUser(credits: 10, purchasedCredits: 50);
        _dynamo.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ThrowsAsync(new ConditionalCheckFailedException("balance moved"));

        var result = await _repository.TryDebitCredits("user-1", 30);

        result.Should().BeFalse();
        _dynamo.Verify(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default), Times.Exactly(2));
    }

    [Fact]
    public async Task TryDebitCredits_ZeroOrNegativeCost_SucceedsWithoutTouchingDynamo()
    {
        var result = await _repository.TryDebitCredits("user-1", 0);

        result.Should().BeTrue();
        _dynamo.Verify(d => d.GetItemAsync(It.IsAny<GetItemRequest>(), default), Times.Never);
    }

    [Fact]
    public async Task TryDebitCredits_UserMissing_Fails()
    {
        _dynamo.Setup(d => d.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse { Item = null, HttpStatusCode = HttpStatusCode.OK });

        var result = await _repository.TryDebitCredits("missing-user", 30);

        result.Should().BeFalse();
    }
}
