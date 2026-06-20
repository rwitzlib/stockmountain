namespace StockMountain.MarketData.Storage;

public interface IBarObjectStore
{
    Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default);

    Task WriteAsync(string objectKey, Stream content, CancellationToken cancellationToken = default);
}
