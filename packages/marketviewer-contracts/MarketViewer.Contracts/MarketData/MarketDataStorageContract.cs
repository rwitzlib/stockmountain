using MarketViewer.Contracts.Enums;

namespace MarketViewer.Contracts.MarketData;

public static class MarketDataStorageContract
{
    public const string DefaultBucketName = "stockmountain-dev-market-data-us-east-2";
    public const string TickerDetailsKey = "tickerdetails/stocks.json";
    public const string AggregatePrefix = "backtest";

    public static string BuildAggregateKey(DateTimeOffset date, int multiplier, Timespan timespan)
    {
        var month = date.Date.Month.ToString("00");
        var day = date.Date.Day.ToString("00");

        return timespan switch
        {
            Timespan.minute => $"{AggregatePrefix}/{date.Date.Year}/{month}/{day}/aggregate_{multiplier}_{timespan}",
            Timespan.hour => $"{AggregatePrefix}/{date.Date.Year}/{month}/aggregate_{multiplier}_{timespan}",
            Timespan.day => $"{AggregatePrefix}/{date.Date.Year}/aggregate_{multiplier}_{timespan}",
            _ => throw new NotSupportedException($"Timespan {timespan} is not supported by market data aggregate storage.")
        };
    }

    public static string BuildInventoryPartitionKey(DateTimeOffset date)
    {
        return $"MARKETDATA#{date.Date:yyyy-MM-dd}";
    }

    public static string BuildAggregateInventorySortKey(int multiplier, Timespan timespan)
    {
        return $"AGGREGATE#{multiplier:D2}#{timespan}";
    }

    public static string BuildRunPartitionKey()
    {
        return "MARKETDATA_RUN";
    }

    public static string BuildRunSortKey(string runId)
    {
        return $"RUN#{runId}";
    }
}
