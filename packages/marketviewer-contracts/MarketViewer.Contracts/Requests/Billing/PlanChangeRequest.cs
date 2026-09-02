namespace MarketViewer.Contracts.Requests.Billing;

public class PlanChangeRequest
{
    /// <summary>Target subscription price key: "Pro", "Premium", "ProAnnual", or "PremiumAnnual".</summary>
    public string Id { get; set; }
}
