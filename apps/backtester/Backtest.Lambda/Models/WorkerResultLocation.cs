namespace Backtest.Lambda.Models;

/// <summary>
/// What the worker lambda actually returns to the orchestrator: a pointer to the full
/// <see cref="MarketViewer.Contracts.Responses.Market.Backtest.WorkerResponse"/> stored in
/// S3. The full response is never returned inline because a signal-heavy day serializes
/// past Lambda's 6MB synchronous response limit, which kills the runtime with a broken
/// pipe while posting the response instead of failing cleanly.
/// </summary>
public class WorkerResultLocation
{
    public DateTimeOffset Date { get; set; }

    /// <summary>S3 key of the stored WorkerResponse. Null when storing failed.</summary>
    public string S3Key { get; set; }

    /// <summary>Set when the day ran but its results could not be stored in S3.</summary>
    public string Error { get; set; }
}
