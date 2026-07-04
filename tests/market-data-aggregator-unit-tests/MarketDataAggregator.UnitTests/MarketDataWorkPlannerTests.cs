using FluentAssertions;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.MarketData;
using Xunit;

namespace MarketDataAggregator.UnitTests;

public class MarketDataWorkPlannerTests
{
    [Fact]
    public void Minute_Should_Produce_One_Item_Per_Trading_Day()
    {
        var items = MarketDataWorkPlanner.BuildWorkDates(
            DateTimeOffset.Parse("2024-06-03"),
            DateTimeOffset.Parse("2024-06-07"),
            [Timespan.minute]).ToList();

        items.Should().HaveCount(5);
        items.Select(item => item.Date.Day).Should().BeEquivalentTo([3, 4, 5, 6, 7]);
        items.Should().OnlyContain(item => item.Timespan == Timespan.minute);
    }

    [Fact]
    public void Weekends_Should_Be_Excluded()
    {
        var items = MarketDataWorkPlanner.BuildWorkDates(
            DateTimeOffset.Parse("2024-06-08"),
            DateTimeOffset.Parse("2024-06-09"),
            [Timespan.minute, Timespan.hour, Timespan.day]).ToList();

        items.Should().BeEmpty();
    }

    [Fact]
    public void Hour_Should_Produce_One_Item_Per_Month_On_Latest_Trading_Day()
    {
        var items = MarketDataWorkPlanner.BuildWorkDates(
            DateTimeOffset.Parse("2024-06-24"),
            DateTimeOffset.Parse("2024-07-05"),
            [Timespan.hour]).ToList();

        items.Should().HaveCount(2);
        items.Select(item => item.Date.Date).Should().BeEquivalentTo(
        [
            DateTime.Parse("2024-06-28"),
            DateTime.Parse("2024-07-05")
        ]);
    }

    [Fact]
    public void Day_Should_Produce_One_Item_Per_Year_On_Latest_Trading_Day()
    {
        var items = MarketDataWorkPlanner.BuildWorkDates(
            DateTimeOffset.Parse("2024-12-30"),
            DateTimeOffset.Parse("2025-01-03"),
            [Timespan.day]).ToList();

        items.Should().HaveCount(2);
        items.Select(item => item.Date.Date).Should().BeEquivalentTo(
        [
            DateTime.Parse("2024-12-31"),
            DateTime.Parse("2025-01-03")
        ]);
    }

    [Fact]
    public void Full_Month_With_All_Timespans_Should_Collapse_Hour_And_Day()
    {
        var items = MarketDataWorkPlanner.BuildWorkDates(
            DateTimeOffset.Parse("2024-06-03"),
            DateTimeOffset.Parse("2024-06-28"),
            [Timespan.minute, Timespan.hour, Timespan.day]).ToList();

        items.Count(item => item.Timespan == Timespan.minute).Should().Be(20);
        items.Count(item => item.Timespan == Timespan.hour).Should().Be(1);
        items.Count(item => item.Timespan == Timespan.day).Should().Be(1);

        items.Single(item => item.Timespan == Timespan.hour).Date.Date.Should().Be(DateTime.Parse("2024-06-28"));
        items.Single(item => item.Timespan == Timespan.day).Date.Date.Should().Be(DateTime.Parse("2024-06-28"));
    }
}
