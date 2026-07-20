using MarketViewer.Contracts.Models.Strategy;
using MarketViewer.Contracts.Responses.Market.Backtest;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models.Backtest;

/// <summary>
/// Everything an anonymous viewer needs to render a shared backtest. Written once to
/// S3 (shares/{shareId}.json) and served verbatim by the unauthenticated share endpoint,
/// so it must never contain the owner's user id, the backtest id, or — when the config
/// is masked — any strategy configuration values.
/// </summary>
[ExcludeFromCodeCoverage]
public class BacktestSharePayload
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Display value only; actual deletion is the S3 lifecycle rule on shares/.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Optional owner-supplied title. Never defaults to anything identifying.</summary>
    public string Title { get; set; }

    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }

    public BacktestShareConfig Config { get; set; }

    /// <summary>Portfolio outcome with Id/CreditsUsed stripped.</summary>
    public BacktestResultResponse Result { get; set; }

    /// <summary>SPY daily closes for the backtest window; null when the fetch failed.</summary>
    public List<BacktestShareBenchmarkPoint> Benchmark { get; set; }
}

/// <summary>
/// Exactly one branch is populated: full settings when the owner chose to include the
/// strategy configuration, otherwise only counts/flags for the locked teaser panels.
/// </summary>
[ExcludeFromCodeCoverage]
public class BacktestShareConfig
{
    public bool Masked { get; set; }

    // Masked == false
    public StrategyPositionSettings PositionSettings { get; set; }
    public StrategyExitSettings ExitSettings { get; set; }
    public StrategyEntrySettings EntrySettings { get; set; }

    // Masked == true
    public int? EntryFilterCount { get; set; }
    public bool? HasStopLoss { get; set; }
    public bool? HasProfitTarget { get; set; }
    public bool? HasTimedExit { get; set; }
}

[ExcludeFromCodeCoverage]
public class BacktestShareBenchmarkPoint
{
    public DateTimeOffset Date { get; set; }
    public float Close { get; set; }
}
