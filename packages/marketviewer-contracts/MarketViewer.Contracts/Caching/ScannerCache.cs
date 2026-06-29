
using MarketViewer.Contracts.Models.Strategy;

namespace MarketViewer.Contracts.Caching;

public class ScannerCache
{
    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
    private List<string> _scanSettingsHashes = [];
    private List<StrategyEntrySettings> _strategyEntrySettings = [];

    /// <summary>
    /// Gets a snapshot of the scan settings hashes. Thread-safe for enumeration.
    /// </summary>
    public IReadOnlyList<string> GetScanSettingsHashes()
    {
        _lock.EnterReadLock();
        try
        {
            return _scanSettingsHashes.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets a snapshot of the strategy entry settings. Thread-safe for enumeration.
    /// </summary>
    public IReadOnlyList<StrategyEntrySettings> GetStrategyEntrySettings()
    {
        _lock.EnterReadLock();
        try
        {
            return _strategyEntrySettings.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Adds a strategy entry setting and its hash if not already present. Thread-safe.
    /// </summary>
    /// <returns>True if added, false if already exists.</returns>
    public bool TryAddStrategy(string hash, StrategyEntrySettings entrySettings)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_scanSettingsHashes.Any(q => q.Equals(hash, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            _scanSettingsHashes.Add(hash);
            _strategyEntrySettings.Add(entrySettings);
            return true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Replaces all cached strategies with a new set. Thread-safe.
    /// </summary>
    public void ReplaceAll(IEnumerable<StrategyEntrySettings> entrySettings)
    {
        _lock.EnterWriteLock();
        try
        {
            var entrySettingsList = entrySettings.ToList();
            _strategyEntrySettings = entrySettingsList;
            _scanSettingsHashes = entrySettingsList
                .Select(q => q.ComputeStrategyHash())
                .ToList();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
}
