namespace StockMountain.MarketData.Storage;

public sealed class S3BarObjectStoreOptions
{
    public required string BucketName { get; init; }

    public string? ServiceUrl { get; init; }

    public bool ForcePathStyle { get; init; }
}
