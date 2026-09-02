using FluentAssertions;
using MarketViewer.Api.Services.Billing;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Responses.Billing;
using Xunit;

namespace MarketViewer.Api.UnitTests.Services;

public class PlanChangeClassificationUnitTests
{
    [Theory]
    [InlineData(UserRole.Pro, BillingInterval.Month, UserRole.Premium, BillingInterval.Month, PlanChangeTiming.Immediate)]
    [InlineData(UserRole.Pro, BillingInterval.Month, UserRole.Premium, BillingInterval.Year, PlanChangeTiming.Immediate)]
    [InlineData(UserRole.Pro, BillingInterval.Year, UserRole.Premium, BillingInterval.Month, PlanChangeTiming.Immediate)]
    [InlineData(UserRole.Pro, BillingInterval.Month, UserRole.Pro, BillingInterval.Year, PlanChangeTiming.Immediate)]
    [InlineData(UserRole.Premium, BillingInterval.Month, UserRole.Pro, BillingInterval.Month, PlanChangeTiming.PeriodEnd)]
    [InlineData(UserRole.Premium, BillingInterval.Month, UserRole.Pro, BillingInterval.Year, PlanChangeTiming.PeriodEnd)]
    [InlineData(UserRole.Premium, BillingInterval.Year, UserRole.Pro, BillingInterval.Year, PlanChangeTiming.PeriodEnd)]
    [InlineData(UserRole.Pro, BillingInterval.Year, UserRole.Pro, BillingInterval.Month, PlanChangeTiming.PeriodEnd)]
    [InlineData(UserRole.Pro, BillingInterval.Month, UserRole.Pro, BillingInterval.Month, null)]
    [InlineData(UserRole.Premium, BillingInterval.Year, UserRole.Premium, BillingInterval.Year, null)]
    public void ClassifyPlanChange_MoreIsImmediate_LessWaitsForPeriodEnd(
        UserRole currentTier, string currentInterval, UserRole targetTier, string targetInterval, string expected)
    {
        BillingCatalog.ClassifyPlanChange(currentTier, currentInterval, targetTier, targetInterval).Should().Be(expected);
    }
}
