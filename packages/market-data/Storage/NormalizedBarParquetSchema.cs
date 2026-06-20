using Parquet.Schema;

namespace StockMountain.MarketData.Storage;

internal static class NormalizedBarParquetSchema
{
    public const string TimestampUtcField = "timestamp_utc";
    public const string OpenField = "open";
    public const string HighField = "high";
    public const string LowField = "low";
    public const string CloseField = "close";
    public const string VolumeField = "volume";
    public const string VwapField = "vwap";
    public const string TransactionCountField = "transaction_count";

    public static ParquetSchema Create() =>
        new(
            new DateTimeDataField(TimestampUtcField, DateTimeFormat.DateAndTime, isNullable: false),
            new DecimalDataField(OpenField, precision: 18, scale: 8, isNullable: false),
            new DecimalDataField(HighField, precision: 18, scale: 8, isNullable: false),
            new DecimalDataField(LowField, precision: 18, scale: 8, isNullable: false),
            new DecimalDataField(CloseField, precision: 18, scale: 8, isNullable: false),
            new DataField<long>(VolumeField, nullable: false),
            new DecimalDataField(VwapField, precision: 18, scale: 8, isNullable: false),
            new DataField<int>(TransactionCountField, nullable: false));
}
