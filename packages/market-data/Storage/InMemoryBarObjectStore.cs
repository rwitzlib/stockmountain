using System.Collections.Concurrent;

namespace StockMountain.MarketData.Storage;

public sealed class InMemoryBarObjectStore : IBarObjectStore
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new(StringComparer.Ordinal);

    public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(_objects.ContainsKey(objectKey));

    public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (!_objects.TryGetValue(objectKey, out var content))
        {
            throw new FileNotFoundException($"Object '{objectKey}' was not found.");
        }

        return Task.FromResult<Stream>(new MemoryStream(content, writable: false));
    }

    public Task WriteAsync(string objectKey, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        _objects[objectKey] = buffer.ToArray();
        return Task.CompletedTask;
    }
}
