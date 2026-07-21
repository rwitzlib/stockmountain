using Massive.Client.Responses;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace MarketViewer.Api.HostedServices;

public enum WarmupStatus
{
    NotStarted,
    Warming,
    Ready,
    Failed
}

/// <summary>
/// Tracks cache warmup state and buffers snapshot responses while a warmup is running.
/// This allows snapshot collection to run in parallel with aggregate population,
/// preventing data gaps when aggregates take longer than 1 minute to load.
/// </summary>
public class CacheWarmupState
{
    /// <summary>
    /// Bounds the snapshot buffer so a warmup that never completes (e.g. persistent
    /// S3 outage) can't grow memory unboundedly; oldest snapshots are dropped first.
    /// </summary>
    private const int MaxBufferedSnapshots = 480;

    private readonly ConcurrentQueue<MassiveSnapshotResponse> _snapshotBuffer = new();
    private readonly object _lock = new();

    public CacheWarmupState(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("MarketViewer.Market");
        meter.CreateObservableGauge("marketviewer.cache.warmup_age_seconds", () =>
            LastSuccessAt is { } lastSuccess ? (DateTimeOffset.Now - lastSuccess).TotalSeconds : -1);
    }

    public WarmupStatus Status { get; private set; } = WarmupStatus.NotStarted;

    public DateTimeOffset? LastSuccessAt { get; private set; }

    public string? LastError { get; private set; }

    /// <summary>
    /// True once a warmup has succeeded and no rebuild is currently in progress.
    /// While false, snapshot data should be buffered instead of merged.
    /// </summary>
    public bool IsReady => Status == WarmupStatus.Ready;

    public void MarkWarming()
    {
        lock (_lock)
        {
            Status = WarmupStatus.Warming;
        }
    }

    public void MarkReady()
    {
        lock (_lock)
        {
            Status = WarmupStatus.Ready;
            LastSuccessAt = DateTimeOffset.Now;
            LastError = null;
        }
    }

    public void MarkFailed(string error)
    {
        lock (_lock)
        {
            LastError = error;

            // A failed *re*-warm keeps serving the previous (stale) data; readiness
            // reports Degraded based on LastSuccessAt age rather than going dark.
            Status = LastSuccessAt is null ? WarmupStatus.Failed : WarmupStatus.Ready;
        }
    }

    /// <summary>
    /// Buffers a snapshot response to be processed after warmup completes.
    /// </summary>
    public void BufferSnapshot(MassiveSnapshotResponse snapshot)
    {
        _snapshotBuffer.Enqueue(snapshot);

        while (_snapshotBuffer.Count > MaxBufferedSnapshots && _snapshotBuffer.TryDequeue(out _))
        {
        }
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
    /// Gets the current count of buffered snapshots.
    /// </summary>
    public int BufferedCount => _snapshotBuffer.Count;
}
