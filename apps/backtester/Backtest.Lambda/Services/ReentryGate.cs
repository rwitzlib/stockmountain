namespace Backtest.Lambda.Services;

/// <summary>
/// Decides, per ticker and per exit type ("hold"/"high"), whether a scan signal is worth
/// pricing, mirroring live buy eligibility: a ticker with an open position cannot be
/// re-bought unless AllowSimultaneous, and every close starts a cooldown (close time +
/// cooldown duration) that blocks new entries until it expires.
///
/// This gate assumes every eligible signal is actually taken, which the portfolio
/// simulator may later refuse (funds, concurrency). That can suppress a re-entry the
/// live system would have made, but it keeps the worker from pricing every signal
/// minute — the simulator re-applies the same rules against actual simulated fills.
/// </summary>
internal sealed class ReentryGate(bool allowSimultaneous, TimeSpan cooldown)
{
    private sealed class ExitTypeState
    {
        // Close times of positions taken on this timeline; a close can be earlier than
        // one recorded before it when AllowSimultaneous stacks positions.
        public List<DateTimeOffset> PendingCloses { get; } = [];
        public DateTimeOffset CooldownExpiry { get; set; } = DateTimeOffset.MinValue;
    }

    private readonly Dictionary<string, ExitTypeState> _states = [];

    public bool IsEligible(string exitType, DateTimeOffset time)
    {
        var state = GetState(exitType);

        // Apply closes that have happened by now; the latest one drives the cooldown,
        // matching live where each close overwrites the ticker's cooldown expiry.
        var latestClose = DateTimeOffset.MinValue;
        state.PendingCloses.RemoveAll(close =>
        {
            if (close > time)
            {
                return false;
            }

            latestClose = close > latestClose ? close : latestClose;
            return true;
        });

        if (latestClose > DateTimeOffset.MinValue)
        {
            var expiry = latestClose + cooldown;
            state.CooldownExpiry = expiry > state.CooldownExpiry ? expiry : state.CooldownExpiry;
        }

        if (!allowSimultaneous && state.PendingCloses.Count > 0)
        {
            return false;
        }

        return time >= state.CooldownExpiry;
    }

    public void RecordFill(string exitType, DateTimeOffset soldAt)
    {
        GetState(exitType).PendingCloses.Add(soldAt);
    }

    private ExitTypeState GetState(string exitType)
    {
        if (!_states.TryGetValue(exitType, out var state))
        {
            state = new ExitTypeState();
            _states[exitType] = state;
        }

        return state;
    }
}
