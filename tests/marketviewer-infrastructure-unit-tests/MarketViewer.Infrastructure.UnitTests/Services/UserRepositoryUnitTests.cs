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

    [Fact]
    public async Task SetStripeCustomerId_OnlyLinksFirstWriter()
    {
        UpdateItemRequest captured = null;
        _dynamo.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .Callback<UpdateItemRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new UpdateItemResponse { HttpStatusCode = HttpStatusCode.OK });

        var result = await _repository.SetStripeCustomerId("user-1", "cus_1");

        result.Should().BeTrue();
        captured.UpdateExpression.Should().Be("SET StripeCustomerId = :customerId");
        captured.ConditionExpression.Should().Be("attribute_exists(Id) AND (attribute_not_exists(StripeCustomerId) OR StripeCustomerId = :customerId)");
        captured.ExpressionAttributeValues[":customerId"].S.Should().Be("cus_1");
    }

    [Fact]
    public async Task SetStripeCustomerId_AlreadyLinkedToDifferentCustomer_ReturnsFalseWithoutOverwriting()
    {
        _dynamo.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ThrowsAsync(new ConditionalCheckFailedException("already linked"));

        var result = await _repository.SetStripeCustomerId("user-1", "cus_other");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ApplySubscriptionGrant_ResetsMonthlyBalanceAndRole()
    {
        UpdateItemRequest captured = null;
        _dynamo.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .Callback<UpdateItemRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new UpdateItemResponse { HttpStatusCode = HttpStatusCode.OK });

        var result = await _repository.ApplySubscriptionGrant("user-1", UserRole.Pro, 1000);

        result.Should().BeTrue();
        captured.UpdateExpression.Should().Be("SET #role = :role, Credits = :grant, MaxCredits = :grant, SubscriptionStatus = :status");
        captured.ExpressionAttributeValues[":role"].S.Should().Be("Pro");
        captured.ExpressionAttributeValues[":grant"].N.Should().Be("1000");
        captured.ExpressionAttributeValues[":status"].S.Should().Be("active");
    }

    [Fact]
    public async Task ApplyUpgradeGrant_AddsDeltaOnTopOfRemainingBalance()
    {
        UpdateItemRequest captured = null;
        _dynamo.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .Callback<UpdateItemRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new UpdateItemResponse { HttpStatusCode = HttpStatusCode.OK });

        var result = await _repository.ApplyUpgradeGrant("user-1", UserRole.Premium, 5000, 4000);

        result.Should().BeTrue();
        captured.UpdateExpression.Should().Be("SET #role = :role, MaxCredits = :grant, SubscriptionStatus = :status ADD Credits :delta");
        captured.ExpressionAttributeValues[":role"].S.Should().Be("Premium");
        captured.ExpressionAttributeValues[":grant"].N.Should().Be("5000");
        captured.ExpressionAttributeValues[":delta"].N.Should().Be("4000");
    }

    [Fact]
    public async Task AddPurchasedCredits_UsesAtomicAdd()
    {
        UpdateItemRequest captured = null;
        _dynamo.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .Callback<UpdateItemRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new UpdateItemResponse { HttpStatusCode = HttpStatusCode.OK });

        var result = await _repository.AddPurchasedCredits("user-1", 250);

        result.Should().BeTrue();
        captured.UpdateExpression.Should().Be("ADD PurchasedCredits :credits");
        captured.ConditionExpression.Should().Be("attribute_exists(Id)");
        captured.ExpressionAttributeValues[":credits"].N.Should().Be("250");
    }

    [Fact]
    public async Task AddPurchasedCredits_MissingUser_ReturnsFalse()
    {
        _dynamo.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ThrowsAsync(new ConditionalCheckFailedException("no record"));

        var result = await _repository.AddPurchasedCredits("missing-user", 250);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CancelSubscription_ClampsMonthlyCreditsToFreeGrant()
    {
        SetupUser(credits: 3000, purchasedCredits: 500, role: "Premium");
        UpdateItemRequest captured = null;
        _dynamo.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .Callback<UpdateItemRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new UpdateItemResponse { HttpStatusCode = HttpStatusCode.OK });

        var result = await _repository.CancelSubscription("user-1", 100);

        result.Should().BeTrue();
        captured.ExpressionAttributeValues[":role"].S.Should().Be("Free");
        captured.ExpressionAttributeValues[":credits"].N.Should().Be("100");
        captured.ExpressionAttributeValues[":maxCredits"].N.Should().Be("100");
        captured.ExpressionAttributeValues[":status"].S.Should().Be("canceled");
        captured.ConditionExpression.Should().Contain("Credits = :expectedCredits");
        captured.ExpressionAttributeValues[":expectedCredits"].N.Should().Be("3000");
    }

    [Fact]
    public async Task CancelSubscription_BalanceBelowFreeGrant_IsKept()
    {
        SetupUser(credits: 40, purchasedCredits: 0, role: "Pro");
        UpdateItemRequest captured = null;
        _dynamo.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .Callback<UpdateItemRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new UpdateItemResponse { HttpStatusCode = HttpStatusCode.OK });

        var result = await _repository.CancelSubscription("user-1", 100);

        result.Should().BeTrue();
        captured.ExpressionAttributeValues[":credits"].N.Should().Be("40");
    }

    [Fact]
    public async Task CancelSubscription_ConcurrentBalanceChange_RetriesOnce()
    {
        SetupUser(credits: 3000, purchasedCredits: 0, role: "Premium");
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

        var result = await _repository.CancelSubscription("user-1", 100);

        result.Should().BeTrue();
        calls.Should().Be(2);
    }
}
