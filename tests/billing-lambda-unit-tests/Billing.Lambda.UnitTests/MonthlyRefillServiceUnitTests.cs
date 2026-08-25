using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Billing.Lambda;
using FluentAssertions;
using MarketViewer.Contracts.Records;
using MarketViewer.Core.Services;
using MarketViewer.Infrastructure.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;

namespace Billing.Lambda.UnitTests;

public class MonthlyRefillServiceUnitTests
{
    private const string Period = "2026-09";
    private const float FreeGrant = 100;

    private readonly Mock<IAmazonDynamoDB> _dynamo = new();
    private readonly Mock<IBillingLedgerRepository> _ledger = new();
    private readonly MonthlyRefillService _service;

    public MonthlyRefillServiceUnitTests()
    {
        _service = new MonthlyRefillService(
            _dynamo.Object,
            new UserConfig { TableName = "user-store" },
            _ledger.Object,
            NullLogger<MonthlyRefillService>.Instance);

        _ledger.Setup(l => l.TryAppend(It.IsAny<BillingLedgerRecord>())).ReturnsAsync(true);
        _ledger.Setup(l => l.IsPending(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        _ledger.Setup(l => l.MarkApplied(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        _dynamo.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse { HttpStatusCode = HttpStatusCode.OK });
    }

    private static ScanResponse Page(Dictionary<string, AttributeValue> lastKey = null, params string[] userIds) => new()
    {
        Items = userIds
            .Select(id => new Dictionary<string, AttributeValue> { { "Id", new AttributeValue { S = id } } })
            .ToList(),
        LastEvaluatedKey = lastKey
    };

    private void SetupSinglePage(params string[] userIds)
    {
        _dynamo.Setup(d => d.ScanAsync(It.IsAny<ScanRequest>(), default))
            .ReturnsAsync(Page(null, userIds));
    }

    [Fact]
    public async Task Run_ScansOnlyFreeTierUsersWithoutActiveSubscription()
    {
        ScanRequest captured = null;
        _dynamo.Setup(d => d.ScanAsync(It.IsAny<ScanRequest>(), default))
            .Callback<ScanRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(Page());

        await _service.Run(Period, FreeGrant, dryRun: false);

        captured.TableName.Should().Be("user-store");
        captured.FilterExpression.Should().Be(
            "(#role = :free OR #role = :basic) AND (attribute_not_exists(SubscriptionStatus) OR SubscriptionStatus <> :active)");
        captured.ExpressionAttributeNames["#role"].Should().Be("Role");
        captured.ExpressionAttributeValues[":free"].S.Should().Be("Free");
        captured.ExpressionAttributeValues[":basic"].S.Should().Be("Basic");
        captured.ExpressionAttributeValues[":active"].S.Should().Be("active");
    }

    [Fact]
    public async Task Run_RefillsEligibleUser_PendingLedgerThenGuardedUpdateThenApplied()
    {
        SetupSinglePage("user-1");

        BillingLedgerRecord ledgerEntry = null;
        _ledger.Setup(l => l.TryAppend(It.IsAny<BillingLedgerRecord>()))
            .Callback<BillingLedgerRecord>(e => ledgerEntry = e)
            .ReturnsAsync(true);

        UpdateItemRequest update = null;
        _dynamo.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .Callback<UpdateItemRequest, CancellationToken>((r, _) => update = r)
            .ReturnsAsync(new UpdateItemResponse { HttpStatusCode = HttpStatusCode.OK });

        var result = await _service.Run(Period, FreeGrant, dryRun: false);

        result.Eligible.Should().Be(1);
        result.Refilled.Should().Be(1);
        result.Failed.Should().Be(0);

        ledgerEntry.UserId.Should().Be("user-1");
        ledgerEntry.EventKey.Should().Be("2026-09#user-1");
        ledgerEntry.Type.Should().Be(BillingLedgerEntryType.MonthlyRefill);
        ledgerEntry.AmountCents.Should().Be(0);
        ledgerEntry.Credits.Should().Be(FreeGrant);
        ledgerEntry.Tier.Should().Be("Free");
        ledgerEntry.Status.Should().Be(BillingLedgerStatus.Pending);

        update.TableName.Should().Be("user-store");
        update.Key["Id"].S.Should().Be("user-1");
        update.UpdateExpression.Should().Be("SET Credits = :grant, MaxCredits = :grant");
        update.ConditionExpression.Should().Contain("#role = :free OR #role = :basic");
        update.ConditionExpression.Should().Contain("SubscriptionStatus <> :active");
        update.ExpressionAttributeValues[":grant"].N.Should().Be("100");

        _ledger.Verify(l => l.MarkApplied("user-1", "2026-09#user-1"), Times.Once);
    }

    [Fact]
    public async Task Run_ExistingAppliedLedgerEntry_SkipsUpdate()
    {
        SetupSinglePage("user-1");
        _ledger.Setup(l => l.TryAppend(It.IsAny<BillingLedgerRecord>())).ReturnsAsync(false);
        _ledger.Setup(l => l.IsPending("user-1", "2026-09#user-1")).ReturnsAsync(false);

        var result = await _service.Run(Period, FreeGrant, dryRun: false);

        result.AlreadyRefilled.Should().Be(1);
        result.Refilled.Should().Be(0);
        _dynamo.Verify(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default), Times.Never);
    }

    [Fact]
    public async Task Run_ExistingPendingLedgerEntry_ResumesInterruptedRefill()
    {
        // A previous run crashed (or failed) between the ledger append and the credit update;
        // the pending row must be resumed, not read as already-refilled.
        SetupSinglePage("user-1");
        _ledger.Setup(l => l.TryAppend(It.IsAny<BillingLedgerRecord>())).ReturnsAsync(false);
        _ledger.Setup(l => l.IsPending("user-1", "2026-09#user-1")).ReturnsAsync(true);

        var result = await _service.Run(Period, FreeGrant, dryRun: false);

        result.Refilled.Should().Be(1);
        result.AlreadyRefilled.Should().Be(0);
        _dynamo.Verify(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default), Times.Once);
        _ledger.Verify(l => l.MarkApplied("user-1", "2026-09#user-1"), Times.Once);
    }

    [Fact]
    public async Task Run_UserBecameIneligibleBetweenScanAndWrite_RemovesPendingEntry()
    {
        SetupSinglePage("user-1");
        _dynamo.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ThrowsAsync(new ConditionalCheckFailedException("changed"));

        var result = await _service.Run(Period, FreeGrant, dryRun: false);

        result.SkippedConcurrentChange.Should().Be(1);
        result.Refilled.Should().Be(0);
        result.Failed.Should().Be(0);
        _ledger.Verify(l => l.Remove("user-1", "2026-09#user-1"), Times.Once);
        _ledger.Verify(l => l.MarkApplied(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Run_IneligibleUserAndRemoveFails_CountsFailedSoRetryHeals()
    {
        // The row stays pending; the retry conditional-fails the update again and retries the
        // remove, so no manual cleanup is ever needed.
        SetupSinglePage("user-1");
        _dynamo.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ThrowsAsync(new ConditionalCheckFailedException("changed"));
        _ledger.Setup(l => l.Remove("user-1", "2026-09#user-1"))
            .ThrowsAsync(new AmazonDynamoDBException("throttled"));

        var act = () => _service.Run(Period, FreeGrant, dryRun: false);

        await act.Should().ThrowAsync<RefillIncompleteException>();
        _ledger.Verify(l => l.Remove("user-1", "2026-09#user-1"), Times.Once);
    }

    [Fact]
    public async Task Run_UpdateThrows_LeavesPendingEntryAndFailsInvocationAfterFullScan()
    {
        SetupSinglePage("user-1", "user-2");
        _dynamo.SetupSequence(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ThrowsAsync(new AmazonDynamoDBException("boom"))
            .ReturnsAsync(new UpdateItemResponse { HttpStatusCode = HttpStatusCode.OK });

        var act = () => _service.Run(Period, FreeGrant, dryRun: false);

        // The run finishes the scan (user-2 still refilled), leaves user-1's row pending for
        // the retry to resume, then fails the invocation so the retry actually happens.
        (await act.Should().ThrowAsync<RefillIncompleteException>())
            .Which.Message.Should().Contain("1 of 2");
        _ledger.Verify(l => l.Remove(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _ledger.Verify(l => l.MarkApplied("user-2", "2026-09#user-2"), Times.Once);
    }

    [Fact]
    public async Task Run_LedgerAppendThrows_FailsInvocationAfterFullScan()
    {
        SetupSinglePage("user-1", "user-2");
        _ledger.SetupSequence(l => l.TryAppend(It.IsAny<BillingLedgerRecord>()))
            .ThrowsAsync(new AmazonDynamoDBException("boom"))
            .ReturnsAsync(true);

        var act = () => _service.Run(Period, FreeGrant, dryRun: false);

        await act.Should().ThrowAsync<RefillIncompleteException>();
        _ledger.Verify(l => l.MarkApplied("user-2", "2026-09#user-2"), Times.Once);
    }

    [Fact]
    public async Task Run_MarkAppliedThrows_CountsFailedSoRetrySettlesIt()
    {
        // Credits were granted but the row is still pending: the retry re-applies the
        // idempotent SET and marks the row applied.
        SetupSinglePage("user-1");
        _ledger.Setup(l => l.MarkApplied("user-1", "2026-09#user-1"))
            .ThrowsAsync(new AmazonDynamoDBException("throttled"));

        var act = () => _service.Run(Period, FreeGrant, dryRun: false);

        await act.Should().ThrowAsync<RefillIncompleteException>();
        _ledger.Verify(l => l.Remove(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Run_PaginatesUntilLastEvaluatedKeyIsEmpty()
    {
        var lastKey = new Dictionary<string, AttributeValue> { { "Id", new AttributeValue { S = "user-1" } } };
        _dynamo.SetupSequence(d => d.ScanAsync(It.IsAny<ScanRequest>(), default))
            .ReturnsAsync(Page(lastKey, "user-1"))
            .ReturnsAsync(Page(null, "user-2"));

        var result = await _service.Run(Period, FreeGrant, dryRun: false);

        result.Eligible.Should().Be(2);
        result.Refilled.Should().Be(2);
        _dynamo.Verify(d => d.ScanAsync(It.IsAny<ScanRequest>(), default), Times.Exactly(2));
    }

    [Fact]
    public async Task Run_DryRun_WritesNothing()
    {
        SetupSinglePage("user-1", "user-2");

        var result = await _service.Run(Period, FreeGrant, dryRun: true);

        result.Eligible.Should().Be(2);
        result.Refilled.Should().Be(0);
        _ledger.Verify(l => l.TryAppend(It.IsAny<BillingLedgerRecord>()), Times.Never);
        _ledger.Verify(l => l.MarkApplied(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _dynamo.Verify(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task Run_NonPositiveGrant_RefusesToRun(float grant)
    {
        // A missing Tiers config binds to 0; running with it would reset every free user's
        // balance to zero (the exact failure mode decision 1 of the plan guards against).
        var act = () => _service.Run(Period, grant, dryRun: false);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _dynamo.Verify(d => d.ScanAsync(It.IsAny<ScanRequest>(), default), Times.Never);
    }
}
