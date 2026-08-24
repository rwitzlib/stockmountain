using MarketViewer.Contracts.Records;

namespace MarketViewer.Core.Services;

public interface IBillingLedgerRepository
{
    /// <summary>
    /// Appends a ledger entry, keyed by (UserId, EventKey). Returns true when the entry was
    /// written, false when an entry with the same key already exists (webhook redelivery).
    /// Throws on transport errors so callers can surface a retryable failure.
    /// </summary>
    Task<bool> TryAppend(BillingLedgerRecord entry);
}
