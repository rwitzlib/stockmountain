using Massive.Client.Responses;
using System.Collections.Concurrent;

namespace MarketViewer.Api.HostedServices;

/// <summary>
/// Tracks cache warmup state and buffers snapshot responses during the warmup phase.
/// This allows snapshot collection to run in parallel with aggregate population,
/// preventing data gaps when aggregates take longer than 1 minute to load.
/// </summary>
public class CacheWarmupState
{
    private readonly ConcurrentQueue<MassiveSnapshotResponse> _snapshotBuffer = new();
    private volatile bool _isWarmupComplete = false;

    /// <summary>
    /// Gets whether the cache warmup (aggregate population) has completed.
    /// </summary>
    public bool IsWarmupComplete => _isWarmupComplete;

    /// <summary>
    /// Buffers a snapshot response to be processed after warmup completes.
    /// </summary>
    public void BufferSnapshot(MassiveSnapshotResponse snapshot)
    {
        _snapshotBuffer.Enqueue(snapshot);
    }

    /// <summary>
    /// Drains all buffered snapshots and returns them for processing.
    /// </summary>
    public IEnumerable<MassiveSnapshotResponse> DrainBuffer()
    {
        var snapshots = new List<MassiveSnapshotResponse>();
        
        while (_snapshotBuffer.TryDequeue(out var snapshot))
        {
            snapshots.Add(snapshot);
        }
        
        return snapshots;
    }

    /// <summary>
    /// Marks the warmup as complete. After this, snapshots will be processed normally instead of buffered.
    /// </summary>
    public void MarkWarmupComplete()
    {
        _isWarmupComplete = true;
    }

    /// <summary>
    /// Gets the current count of buffered snapshots.
    /// </summary>
    public int BufferedCount => _snapshotBuffer.Count;
}
