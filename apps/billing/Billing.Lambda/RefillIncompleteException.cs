namespace Billing.Lambda;

/// <summary>
/// Thrown after a refill run completes its scan with per-user failures. Failing the invocation
/// triggers Lambda's async retry; the run is idempotent, so the retry only touches users whose
/// ledger rows are missing or still pending.
/// </summary>
public class RefillIncompleteException(string message) : Exception(message);
