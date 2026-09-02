using MarketViewer.Contracts.Enums;

namespace MarketViewer.Contracts.Responses.Billing;

public class PlanChangePreviewResponse
{
    /// <summary>"immediate" (prorated charge now) or "period_end" (scheduled, nothing charged now).</summary>
    public string Timing { get; set; }

    public UserRole NewTier { get; set; }
    public string NewInterval { get; set; }

    /// <summary>Prorated amount charged today for an immediate change; 0 for a period-end change.</summary>
    public long AmountDueCents { get; set; }

    /// <summary>ISO currency code of <see cref="AmountDueCents"/>, e.g. "usd".</summary>
    public string Currency { get; set; }

    public DateTime EffectiveAt { get; set; }
}

public static class PlanChangeTiming
{
    public const string Immediate = "immediate";
    public const string PeriodEnd = "period_end";
}
