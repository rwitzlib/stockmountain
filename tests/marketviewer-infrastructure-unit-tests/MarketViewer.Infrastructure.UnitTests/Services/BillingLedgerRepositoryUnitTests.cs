using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using MarketViewer.Contracts.Records;
using MarketViewer.Infrastructure.Config;
using MarketViewer.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using Xunit;

namespace MarketViewer.Infrastructure.UnitTests.Services;

public class BillingLedgerRepositoryUnitTests
{
    private readonly Mock<IAmazonDynamoDB> _dynamo = new();
    private readonly BillingLedgerRepository _repository;

    public BillingLedgerRepositoryUnitTests()
    {
        _repository = new BillingLedgerRepository(
            new BillingLedgerConfig { TableName = "billing-ledger" },
            _dynamo.Object,
            NullLogger<BillingLedgerRepository>.Instance);
    }

    private static BillingLedgerRecord Entry() => new()
    {
        UserId = "user-1",
        EventKey = "2026-08-24T12:00:00Z#evt_1",
        Type = BillingLedgerEntryType.TopupPurchase,
        AmountCents = 1000,
        Credits = 250,
        StripeEventId = "evt_1",
        StripePaymentIntentId = "pi_1",
        Description = "Credit pack PackSmall"
    };

    [Fact]
    public async Task TryAppend_WritesConditionalPutWithAllAttributes()
    {
        PutItemRequest captured = null;
        _dynamo.Setup(d => d.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new PutItemResponse { HttpStatusCode = HttpStatusCode.OK });

        var result = await _repository.TryAppend(Entry());

        result.Should().BeTrue();
        captured.TableName.Should().Be("billing-ledger");
        captured.ConditionExpression.Should().Be("attribute_not_exists(EventKey)");
        captured.Item["UserId"].S.Should().Be("user-1");
        captured.Item["EventKey"].S.Should().Be("2026-08-24T12:00:00Z#evt_1");
        captured.Item["Type"].S.Should().Be("topup_purchase");
        captured.Item["AmountCents"].N.Should().Be("1000");
        captured.Item["Credits"].N.Should().Be("250");
        captured.Item["StripeEventId"].S.Should().Be("evt_1");
        captured.Item["StripePaymentIntentId"].S.Should().Be("pi_1");
        captured.Item.Should().NotContainKey("StripeInvoiceId");
        captured.Item.Should().NotContainKey("Tier");
    }

    [Fact]
    public async Task TryAppend_NegativeAmount_SerializesSigned()
    {
        PutItemRequest captured = null;
        _dynamo.Setup(d => d.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new PutItemResponse { HttpStatusCode = HttpStatusCode.OK });

        var entry = Entry();
        entry.Type = BillingLedgerEntryType.Refund;
        entry.AmountCents = -1000;
        entry.Credits = 0;

        var result = await _repository.TryAppend(entry);

        result.Should().BeTrue();
        captured.Item["AmountCents"].N.Should().Be("-1000");
    }

    [Fact]
    public async Task TryAppend_DuplicateEventKey_ReturnsFalse()
    {
        _dynamo.Setup(d => d.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ThrowsAsync(new ConditionalCheckFailedException("exists"));

        var result = await _repository.TryAppend(Entry());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Remove_DeletesByUserAndEventKey()
    {
        DeleteItemRequest captured = null;
        _dynamo.Setup(d => d.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), default))
            .Callback<DeleteItemRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new DeleteItemResponse { HttpStatusCode = HttpStatusCode.OK });

        await _repository.Remove("user-1", "2026-08-24T12:00:00Z#evt_1");

        captured.TableName.Should().Be("billing-ledger");
        captured.Key["UserId"].S.Should().Be("user-1");
        captured.Key["EventKey"].S.Should().Be("2026-08-24T12:00:00Z#evt_1");
    }

    [Fact]
    public async Task TryAppend_PendingStatus_IsWritten()
    {
        PutItemRequest captured = null;
        _dynamo.Setup(d => d.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new PutItemResponse { HttpStatusCode = HttpStatusCode.OK });

        var entry = Entry();
        entry.Status = BillingLedgerStatus.Pending;

        await _repository.TryAppend(entry);

        captured.Item["Status"].S.Should().Be("pending");
    }

    [Fact]
    public async Task TryAppend_NoStatus_OmitsAttribute()
    {
        PutItemRequest captured = null;
        _dynamo.Setup(d => d.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .Callback<PutItemRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new PutItemResponse { HttpStatusCode = HttpStatusCode.OK });

        await _repository.TryAppend(Entry());

        captured.Item.Should().NotContainKey("Status");
    }

    [Fact]
    public async Task IsPending_PendingEntry_ReturnsTrue()
    {
        GetItemRequest captured = null;
        _dynamo.Setup(d => d.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .Callback<GetItemRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new GetItemResponse
            {
                Item = new Dictionary<string, AttributeValue>
                {
                    { "Status", new AttributeValue { S = "pending" } }
                }
            });

        var result = await _repository.IsPending("user-1", "2026-09#user-1");

        result.Should().BeTrue();
        captured.TableName.Should().Be("billing-ledger");
        captured.Key["UserId"].S.Should().Be("user-1");
        captured.Key["EventKey"].S.Should().Be("2026-09#user-1");
        captured.ExpressionAttributeNames["#status"].Should().Be("Status");
        captured.ConsistentRead.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IsPending_AppliedOrMissingEntry_ReturnsFalse(bool itemExists)
    {
        _dynamo.Setup(d => d.GetItemAsync(It.IsAny<GetItemRequest>(), default))
            .ReturnsAsync(new GetItemResponse
            {
                Item = itemExists ? [] : null
            });

        var result = await _repository.IsPending("user-1", "2026-09#user-1");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task MarkApplied_RemovesStatusAttribute()
    {
        UpdateItemRequest captured = null;
        _dynamo.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .Callback<UpdateItemRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new UpdateItemResponse { HttpStatusCode = HttpStatusCode.OK });

        await _repository.MarkApplied("user-1", "2026-09#user-1");

        captured.TableName.Should().Be("billing-ledger");
        captured.Key["UserId"].S.Should().Be("user-1");
        captured.Key["EventKey"].S.Should().Be("2026-09#user-1");
        captured.UpdateExpression.Should().Be("REMOVE #status");
        captured.ExpressionAttributeNames["#status"].Should().Be("Status");
        captured.ConditionExpression.Should().Be("attribute_exists(EventKey)");
    }

    [Fact]
    public async Task MarkApplied_TransportError_Throws()
    {
        _dynamo.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ThrowsAsync(new AmazonDynamoDBException("throttled"));

        var act = () => _repository.MarkApplied("user-1", "2026-09#user-1");

        await act.Should().ThrowAsync<AmazonDynamoDBException>();
    }

    [Fact]
    public async Task TryAppend_TransportError_Throws()
    {
        _dynamo.Setup(d => d.PutItemAsync(It.IsAny<PutItemRequest>(), default))
            .ThrowsAsync(new AmazonDynamoDBException("throttled"));

        var act = () => _repository.TryAppend(Entry());

        await act.Should().ThrowAsync<AmazonDynamoDBException>();
    }
}
